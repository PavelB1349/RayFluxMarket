using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RayFluxMarket.Models.Entities;
using RayFluxMarket.Data;
using System.ComponentModel.DataAnnotations;
using RayFluxMarket.Models.DTOs;

using Microsoft.AspNetCore.Authorization;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin")] // <-- По умолчанию ВСЕ методы контроллера теперь только для Админа!
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _context;
    public ProductsController(AppDbContext context)
    {
        _context = context;
    }

    private bool ProductExists(int? id)
    {
        return _context.Products.Any(e => e.Id == id);
    }


    // GET: api/Product
    //[HttpGet]
    //public async Task<ActionResult<IEnumerable<Product>>> GetProduct()
    //{
    //    //return await _context.Products.ToListAsync();
    //    return await _context.Products
    //    .Include(p => p.Images)      // Подтягиваем все фото
    //    .Include(p => p.Materials)   // Подтягиваем материалы
    //    .Include(p => p.Brand)       // Подтягиваем бренд (название, лого и т.д.)
    //    .Include(p => p.Category)    // Подтягиваем категорию
    //    .AsNoTracking() // Для оптимизации чтения, если не планируем изменять эти объекты
    //    .ToListAsync();
    //}
    [HttpGet]
    [AllowAnonymous] // <-- Этот метод теперь доступен всем, даже неавторизованным пользователям
    public async Task<ActionResult<IEnumerable<Product>>> GetProducts([FromQuery] ProductQueryParameters query)
    {
        // 1. Создаем базовый запрос к таблице, подтягивая связанные данные
        var productsQuery = _context.Products
            .Include(p => p.Images)
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .AsNoTracking() // Оптимизация для чтения
            .AsQueryable(); // Переводим в режим динамического построения запроса

        // 2. ФИЛЬТРАЦИЯ: Поиск по названию (без учета регистра)
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var searchLower = query.Search.ToLower();
            productsQuery = productsQuery.Where(p => p.Name.ToLower().Contains(searchLower));
        }

        // 3. ФИЛЬТРАЦИЯ: По Категории и Бренду
        if (query.CategoryId.HasValue)
        {
            productsQuery = productsQuery.Where(p => p.CategoryId == query.CategoryId.Value);
        }

        if (query.BrandId.HasValue)
        {
            productsQuery = productsQuery.Where(p => p.BrandId == query.BrandId.Value);
        }

        // 4. ФИЛЬТРАЦИЯ: По диапазону цен
        if (query.MinPrice.HasValue)
        {
            productsQuery = productsQuery.Where(p => p.Price >= query.MinPrice.Value);
        }

        if (query.MaxPrice.HasValue)
        {
            productsQuery = productsQuery.Where(p => p.Price <= query.MaxPrice.Value);
        }

        // 5. СОРТИРОВКА
        productsQuery = query.SortBy?.ToLower() switch
        {
            "price_asc" => productsQuery.OrderBy(p => p.Price),
            "price_desc" => productsQuery.OrderByDescending(p => p.Price),
            "name_desc" => productsQuery.OrderByDescending(p => p.Name),
            _ => productsQuery.OrderBy(p => p.Id) // Сортировка по умолчанию
        };

        // 6. ПАГИНАЦИЯ (Магия пропуска и взятия строк)
        // Защита от дурака: номер страницы не может быть меньше 1, размер не меньше 1 и не больше 50
        int pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
        int pageSize = query.PageSize < 1 ? 1 : (query.PageSize > 50 ? 50 : query.PageSize);

        var products = await productsQuery
            .Skip((pageNumber - 1) * pageSize) // Пропускаем товары предыдущих страниц
            .Take(pageSize)                   // Берем ровно столько, сколько нужно для текущей страницы
            .ToListAsync();

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

        if (product == null) return NotFound();
        return product;
    }

    // PUT: api/Product/5
    // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
    //[HttpPut("{id}")]
    //public async Task<IActionResult> PutProduct(int? id, Product product)
    //{
    //    if (id != product.Id)
    //    {
    //        return BadRequest();
    //    }

    //    _context.Entry(product).State = EntityState.Modified;

    //    try
    //    {
    //        await _context.SaveChangesAsync();
    //    }
    //    catch (DbUpdateConcurrencyException)
    //    {
    //        if (!ProductExists(id))
    //        {
    //            return NotFound();
    //        }
    //        else
    //        {
    //            throw;
    //        }
    //    }

    //    return NoContent();
    //}
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

        return NoContent(); // Стандартный успешный ответ для PUT (204 No Content)
    }

    // POST: api/Product
    // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
    //[HttpPost]
    //public async Task<ActionResult<Product>> PostProduct(Product product)
    //{
    //    _context.Products.Add(product);
    //    await _context.SaveChangesAsync();

    //    return CreatedAtAction("GetProduct", new { id = product.Id }, product);
    //}
    [HttpPost]
    public async Task<ActionResult<Product>> PostProduct(CreateProductDto dto)
    {
        var product = new Product
        {
            Name = dto.Name,
            Description = dto.Description,
            Price = dto.Price,
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
        //await _context.SaveChangesAsync();

        //// Загружаем связанные данные для красивого ответа
        //await _context.Entry(product).Collection(p => p.Images).LoadAsync();
        //await _context.Entry(product).Collection(p => p.Materials).LoadAsync();
        //await _context.Entry(product).Reference(p => p.Brand).LoadAsync();

        //return CreatedAtAction("GetProduct", new { id = product.Id }, product);
        await _context.SaveChangesAsync();

        // Перезапрашиваем продукт со всеми связями для чистого ответа
        var completeProduct = await _context.Products
            .Include(p => p.Images)
            .Include(p => p.Materials)
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == product.Id);

        return CreatedAtAction("GetProduct", new { id = product.Id }, completeProduct);
    }

    // DELETE: api/Product/5
    //[HttpDelete("{id}")]
    //public async Task<IActionResult> DeleteProduct(int? id)
    //{
    //    var product = await _context.Products.FindAsync(id);
    //    if (product == null)
    //    {
    //        return NotFound();
    //    }

    //    _context.Products.Remove(product);
    //    await _context.SaveChangesAsync();

    //    return NoContent();
    //}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        // Ищем продукт вместе с его картинками
        var product = await _context.Products
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
        {
            return NotFound(new { message = $"Товар с ID {id} не найден." });
        }

        // Если каскадное удаление на уровне БД вдруг не сработает,
        // мы явно удаляем связанные картинки из контекста перед удалением товара
        if (product.Images.Any())
        {
            _context.ProductImages.RemoveRange(product.Images);
        }

        // Удаляем сам товар
        _context.Products.Remove(product);

        // Сохраняем изменения в базе
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
