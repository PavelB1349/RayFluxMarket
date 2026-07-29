using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RayFluxMarket.Models.Entities;
using RayFluxMarket.Data;
using System.ComponentModel.DataAnnotations;
using RayFluxMarket.Models.DTOs;
using RayFluxMarket.Services;
using Microsoft.Extensions.Caching.Memory;

using Microsoft.AspNetCore.Authorization;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin")] // <-- По умолчанию ВСЕ методы контроллера теперь только для Админа!
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache; // <-- Добавили поле для кэша
    public ProductsController(AppDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    private bool ProductExists(int? id)
    {
        return _context.Products.Any(e => e.Id == id);
    }


    // GET: api/Products
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<Product>>> GetProducts([FromQuery] ProductQueryParameters query)
    {
        // 1. Формируем уникальный ключ кэша на основе всех фильтров клиента
        string cacheKey = $"products_p{query.PageNumber}_s{query.PageSize}_q{query.Search}_c{query.CategoryId}_b{query.BrandId}_min{query.MinPrice}_max{query.MaxPrice}_sort{query.SortBy}";

        // 2. Пытаемся достать готовые данные из оперативной памяти
        if (_cache.TryGetValue(cacheKey, out IEnumerable<Product>? cachedProducts))
        {
            // Если нашли — отдаем мгновенно, минуя базу данных!
            return Ok(cachedProducts);
        }

        // 3. ЕСЛИ В КЭШЕ ПУСТО — ДЕЛАЕМ ЗАПРОС К БАЗЕ ДАННЫХ
        var productsQuery = _context.Products
            .Include(p => p.Images)
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Where(p => p.IsActive)// <-- Фильтруем только активные товары
            .AsNoTracking()
            .AsQueryable();

        // (ЗДЕСЬ ОСТАЕТСЯ ВЕСЬ ТВОЙ КОД ФИЛЬТРАЦИИ ИЗ ПРОШЛОЙ ЗАДАЧИ)
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

        // 4. СОХРАНЯЕМ РЕЗУЛЬТАТ В КЭШ ПЕРЕД ОТПРАВКОЙ КЛИЕНТУ
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(2)); // Храним данные ровно 2 минуты

        _cache.Set(cacheKey, products, cacheOptions);

        return Ok(products);
    }


    [HttpGet("{id}")]
    [AllowAnonymous] // <-- Этот метод теперь доступен всем, даже неавторизованным пользователям
    public async Task<ActionResult<Product>> GetProduct(int id)
    {
        var product = await _context.Products
            .Include(p => p.Images)
            .Include(p => p.Materials)
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null || !product.IsActive) return NotFound(new { message = "Товар не найден или удален." });
        return product;
    }

    
    [HttpPut("{id}")]
    public async Task<IActionResult> PutProduct(int id, CreateProductDto dto)
    {
        // 1. Ищем существующий товар в базе вместе со всеми его связями
        var product = await _context.Products
            .Include(p => p.Images)
            .Include(p => p.Materials)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
        {
            return NotFound(new { message = $"Товар с ID {id} не найден." });
        }

        // 2. Обновляем простые свойства
        product.Name = dto.Name;
        product.Description = dto.Description;
        product.Price = dto.Price;
        product.StockQuantity = dto.StockQuantity;
        product.Season = dto.Season;
        product.Collection = dto.Collection;
        product.CategoryId = dto.CategoryId;
        product.BrandId = dto.BrandId;

        // 3. Обновляем картинки (простой и надежный способ: трем старые, пишем новые)
        if (product.Images.Any())
        {
            _context.ProductImages.RemoveRange(product.Images);
        }

        if (dto.ImageUrls != null && dto.ImageUrls.Any())
        {
            foreach (var url in dto.ImageUrls)
            {
                product.Images.Add(new ProductImage
                {
                    Url = url,
                    IsPrimary = (product.Images.Count == 0)
                });
            }
        }

        // 4. Обновляем материалы
        product.Materials.Clear(); // Сбрасываем старые связи Many-to-Many
        if (dto.MaterialIds != null && dto.MaterialIds.Any())
        {
            var materials = await _context.Materials
                .Where(m => dto.MaterialIds.Contains(m.Id))
                .ToListAsync();

            product.Materials = materials;
        }

        // 5. Сохраняем всё это добро
        await _context.SaveChangesAsync();

        ClearProductsCache();

        return NoContent(); // Стандартный успешный ответ для PUT (204 No Content)
    }

    [HttpPost]
    public async Task<ActionResult<Product>> PostProduct(CreateProductDto dto)
    {
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
            CreatedAt = DateTime.UtcNow
        };

        // Обработка списка картинок 
        if (dto.ImageUrls != null && dto.ImageUrls.Any())
        {
            foreach (var url in dto.ImageUrls)
            {
                product.Images.Add(new ProductImage
                {
                    Url = url,
                    // Первую картинку в списке делаем основной (IsPrimary = true), остальные — нет
                    IsPrimary = (product.Images.Count == 0)
                });
            }
        }

        // Привязка материалов
        if (dto.MaterialIds != null && dto.MaterialIds.Any())
        {
            var materials = await _context.Materials
                .Where(m => dto.MaterialIds.Contains(m.Id))
                .ToListAsync();

            product.Materials = materials;
        }

        _context.Products.Add(product);
       
        await _context.SaveChangesAsync();

        // Перезапрашиваем продукт со всеми связями для чистого ответа
        var completeProduct = await _context.Products
            .Include(p => p.Images)
            .Include(p => p.Materials)
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == product.Id);

        ClearProductsCache();

        return CreatedAtAction("GetProduct", new { id = product.Id }, completeProduct);
    }


    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProduct(int id, [FromServices] IFileService fileService)
    {
        var product = await _context.Products
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
        {
            return NotFound(new { message = $"Товар с ID {id} не найден." });
        }

        if (product.Images.Any())
        {
            // Удаляем физические файлы с диска сервера!
            foreach (var img in product.Images)
            {
                fileService.DeleteProductImage(img.Url);
            }
            _context.ProductImages.RemoveRange(product.Images);
        }

        // Мягкое удаление: не удаляем физически из базы, а делаем неактивным
        product.IsActive = false;
        await _context.SaveChangesAsync();

        ClearProductsCache(); // <-- Очищаем кэш после удаления товара, чтобы клиенты не видели устаревшие данные

        return NoContent();
    }

    // POST: api/Products/{id}/image (Загрузка картинки к товару — ТОЛЬКО ДЛЯ АДМИНА)
    [HttpPost("{id}/image")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UploadProductImage(int id, IFormFile file, [FromServices] IFileService fileService)
    {
        // 1. Ищем товар в базе
        var product = await _context.Products.FindAsync(id);
        if (product == null)
        {
            return NotFound(new { message = $"Товар с ID {id} не найден." });
        }

        try
        {
            // 2. Отдаем файл сервису на сохранение и получаем путь к нему
            var relativePath = await fileService.UploadProductImageAsync(file);

            // 3. Проверяем, есть ли уже картинки у этого товара
            bool hasImages = await _context.ProductImages.AnyAsync(i => i.ProductId == id);

            // 4. Создаем запись в базе данных
            var productImage = new ProductImage
            {
                ProductId = id,
                Url = relativePath,
                IsPrimary = !hasImages // Если картинок еще не было, эта автоматически становится главной
            };

            _context.ProductImages.Add(productImage);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Картинка успешно загружена.", url = relativePath });
        }
        catch (ArgumentException ex)
        {
            // Если сервис выкинул ошибку (например, не тот формат), отдаем 400 BadRequest
            return BadRequest(new { message = ex.Message });
        }
    }
    private void ClearProductsCache()
    {
        // Приводим IMemoryCache к MemoryCache, чтобы получить доступ к методу Compact
        if (_cache is MemoryCache memoryCache)
        {
            // 1.0 означает 100%. Мы вычищаем весь кэш из оперативной памяти.
            memoryCache.Compact(1.0);
        }
    }
}
