using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RayFluxMarket.Data;
using RayFluxMarket.Models.DTOs;
using RayFluxMarket.Models.Entities;

namespace RayFluxMarket.Services
{
    public class ProductService : IProductService
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly IFileService _fileService;

        public ProductService(AppDbContext context, IMemoryCache cache, IFileService fileService)
        {
            _context = context;
            _cache = cache;
            _fileService = fileService;
        }

        public async Task<IEnumerable<ProductDto>> GetProductsAsync(ProductQueryParameters query)
        {
            // Инвариантная культура — чтобы ключ кэша не зависел от локали сервера
            string cacheKey = string.Join("|",
                "products",
                query.PageNumber,
                query.PageSize,
                query.Search?.Trim().ToLowerInvariant() ?? "",
                query.CategoryId?.ToString() ?? "",
                query.BrandId?.ToString() ?? "",
                query.MinPrice?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
                query.MaxPrice?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
                query.SortBy?.ToLowerInvariant() ?? "");

            if (_cache.TryGetValue(cacheKey, out List<ProductDto>? cached))
            {
                return cached!;
            }

            var productsQuery = _context.Products
                .AsNoTracking()
                .Include(p => p.Images)
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .Include(p => p.Materials)
                .Where(p => p.IsActive)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var searchLower = query.Search.ToLower();
                productsQuery = productsQuery.Where(p => p.Name.ToLower().Contains(searchLower));
            }

            if (query.CategoryId.HasValue) productsQuery = productsQuery.Where(p => p.CategoryId == query.CategoryId.Value);
            if (query.BrandId.HasValue) productsQuery = productsQuery.Where(p => p.BrandId == query.BrandId.Value);
            if (query.MinPrice.HasValue) productsQuery = productsQuery.Where(p => p.Price >= query.MinPrice.Value);
            if (query.MaxPrice.HasValue) productsQuery = productsQuery.Where(p => p.Price <= query.MaxPrice.Value);

            productsQuery = query.SortBy?.ToLower() switch
            {
                "price_asc" => productsQuery.OrderBy(p => p.Price),
                "price_desc" => productsQuery.OrderByDescending(p => p.Price),
                "name_desc" => productsQuery.OrderByDescending(p => p.Name),
                _ => productsQuery.OrderBy(p => p.Id)
            };

            int pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
            int pageSize = query.PageSize < 1 ? 1 : (query.PageSize > 50 ? 50 : query.PageSize);

            var products = await productsQuery
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var dtos = products.Select(MapToDto).ToList();

            _cache.Set(cacheKey, dtos, new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(2)));

            return dtos;
        }

        public async Task<ProductDto> GetProductByIdAsync(int id)
        {
            var product = await _context.Products
                .AsNoTracking()
                .Include(p => p.Images)
                .Include(p => p.Materials)
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null || !product.IsActive)
            {
                throw new KeyNotFoundException($"Товар с ID {id} не найден или удален.");
            }

            return MapToDto(product);
        }

        public async Task<ProductDto> CreateProductAsync(CreateProductDto dto)
        {
            await EnsureReferencesExistAsync(dto);

            var product = new Product
            {
                Name = dto.Name,
                Description = dto.Description,
                Price = dto.Price,
                StockQuantity = dto.StockQuantity,
                Season = dto.Season,
                Collection = dto.Collection,
                CategoryId = dto.CategoryId,
                BrandId = dto.BrandId,
                IsActive = dto.IsActive ?? true,
                CreatedAt = DateTime.UtcNow
            };

            if (dto.ImageUrls != null && dto.ImageUrls.Any())
            {
                var imageIndex = 0;
                foreach (var url in dto.ImageUrls)
                {
                    product.Images.Add(new ProductImage
                    {
                        Url = url,
                        IsPrimary = imageIndex == 0
                    });
                    imageIndex++;
                }
            }

            if (dto.MaterialIds != null && dto.MaterialIds.Any())
            {
                product.Materials = await _context.Materials
                    .Where(m => dto.MaterialIds.Contains(m.Id))
                    .ToListAsync();
            }

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            ClearProductsCache();

            // Не используем GetProductByIdAsync: он бросает 404 для IsActive = false,
            // а админ может создать товар сразу снятым с продажи
            var created = await _context.Products
                .AsNoTracking()
                .Include(p => p.Images)
                .Include(p => p.Materials)
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .FirstAsync(p => p.Id == product.Id);

            return MapToDto(created);
        }

        public async Task UpdateProductAsync(int id, CreateProductDto dto)
        {
            var product = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Materials)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                throw new KeyNotFoundException($"Товар с ID {id} не найден.");
            }

            await EnsureReferencesExistAsync(dto);

            product.Name = dto.Name;
            product.Description = dto.Description;
            product.Price = dto.Price;
            product.StockQuantity = dto.StockQuantity;
            product.Season = dto.Season;
            product.Collection = dto.Collection;
            product.CategoryId = dto.CategoryId;
            product.BrandId = dto.BrandId;

            // Явное значение позволяет админу снова включить снятый с продажи товар;
            // null — флаг не трогаем, чтобы не скрыть товар случайно
            if (dto.IsActive.HasValue)
            {
                product.IsActive = dto.IsActive.Value;
            }

            // Запоминаем старые картинки, чтобы после сохранения удалить ненужные файлы с диска
            var oldImageUrls = product.Images.Select(i => i.Url).ToList();

            if (product.Images.Any())
            {
                _context.ProductImages.RemoveRange(product.Images);
            }

            if (dto.ImageUrls != null && dto.ImageUrls.Any())
            {
                // Старые картинки только помечены на удаление и ещё лежат в коллекции,
                // поэтому индекс главного фото считаем локально
                var imageIndex = 0;
                foreach (var url in dto.ImageUrls)
                {
                    product.Images.Add(new ProductImage
                    {
                        Url = url,
                        IsPrimary = imageIndex == 0
                    });
                    imageIndex++;
                }
            }

            product.Materials.Clear();
            if (dto.MaterialIds != null && dto.MaterialIds.Any())
            {
                product.Materials = await _context.Materials
                    .Where(m => dto.MaterialIds.Contains(m.Id))
                    .ToListAsync();
            }

            await _context.SaveChangesAsync();

            // Удаляем файлы картинок, которые больше не используются товаром
            var remainingUrls = dto.ImageUrls ?? new List<string>();
            foreach (var url in oldImageUrls.Except(remainingUrls))
            {
                _fileService.DeleteProductImage(url);
            }

            ClearProductsCache();
        }

        public async Task DeleteProductAsync(int id)
        {
            var product = await _context.Products
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                throw new KeyNotFoundException($"Товар с ID {id} не найден.");
            }

            bool wasOrdered = await _context.OrderItems.AnyAsync(oi => oi.ProductId == id);
            if (!wasOrdered)
            {
                foreach (var img in product.Images)
                {
                    _fileService.DeleteProductImage(img.Url);
                }
                _context.Products.Remove(product);
            }
            else
            {
                // Мягкое удаление: не ломаем историю заказов
                product.IsActive = false;
            }

            await _context.SaveChangesAsync();
            ClearProductsCache();
        }

        public async Task<string> UploadImageAsync(int productId, IFormFile file)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                throw new KeyNotFoundException($"Товар с ID {productId} не найден.");
            }

            var relativePath = await _fileService.UploadProductImageAsync(file);

            bool hasImages = await _context.ProductImages.AnyAsync(i => i.ProductId == productId);

            var productImage = new ProductImage
            {
                ProductId = productId,
                Url = relativePath,
                IsPrimary = !hasImages
            };

            _context.ProductImages.Add(productImage);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch
            {
                // Если запись в БД упала — удаляем уже сохранённый файл, чтобы не копить мусор
                _fileService.DeleteProductImage(relativePath);
                throw;
            }

            ClearProductsCache();
            return relativePath;
        }

        private async Task EnsureReferencesExistAsync(CreateProductDto dto)
        {
            if (!await _context.Categories.AnyAsync(c => c.Id == dto.CategoryId))
            {
                throw new InvalidOperationException($"Категория с ID {dto.CategoryId} не существует.");
            }

            if (!await _context.Brands.AnyAsync(b => b.Id == dto.BrandId))
            {
                throw new InvalidOperationException($"Бренд с ID {dto.BrandId} не существует.");
            }

            if (dto.MaterialIds != null && dto.MaterialIds.Any())
            {
                var existingCount = await _context.Materials
                    .CountAsync(m => dto.MaterialIds.Contains(m.Id));

                if (existingCount != dto.MaterialIds.Distinct().Count())
                {
                    throw new InvalidOperationException("Один или несколько материалов не существуют.");
                }
            }
        }

        private static ProductDto MapToDto(Product p)
        {
            return new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Price = p.Price,
                StockQuantity = p.StockQuantity,
                Season = p.Season,
                Collection = p.Collection,
                IsActive = p.IsActive,
                CreatedAt = p.CreatedAt,
                CategoryId = p.CategoryId,
                CategoryName = p.Category?.Name,
                BrandId = p.BrandId,
                BrandName = p.Brand?.Name,
                ImageUrls = p.Images.Select(i => i.Url).ToList(),
                PrimaryImageUrl = p.Images
                    .OrderByDescending(i => i.IsPrimary)
                    .Select(i => i.Url)
                    .FirstOrDefault(),
                MaterialNames = p.Materials.Select(m => m.Name).ToList()
            };
        }

        private void ClearProductsCache()
        {
            if (_cache is MemoryCache memoryCache)
            {
                memoryCache.Compact(1.0);
            }
        }
    }
}
