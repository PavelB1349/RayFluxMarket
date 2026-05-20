using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RayFluxMarket.Models.Entities;
using RayFluxMarket.Data;
using System.ComponentModel.DataAnnotations;

[Route("api/[controller]")]
[ApiController]
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
    public async Task<ActionResult<IEnumerable<Product>>> GetProducts(
    [FromQuery] string? search = null,
    [FromQuery] int? categoryId = null,
    [FromQuery] int? brandId = null,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 10)
    {
        // 1. Создаем базовый запрос к базе данных (данные еще не скачиваются)
        var query = _context.Products
            .Include(p => p.Images)
            .Include(p => p.Materials)
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .AsNoTracking()
            .AsQueryable(); // Превращаем в Queryable, чтобы динамически строить SQL-запрос

        // 2. Фильтр по поисковой строке (без учета регистра)
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(p => p.Name.ToLower().Contains(search.ToLower()));
        }

        // 3. Фильтр по категории
        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        // 4. Фильтр по бренду
        if (brandId.HasValue)
        {
            query = query.Where(p => p.BrandId == brandId.Value);
        }

        // 5. Пагинация (пропускаем старые страницы, берем размер текущей)
        var products = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(products);
    }

    // GET: api/Product/5
    //[HttpGet("{id}")]
    //public async Task<ActionResult<Product>> GetProduct(int id)
    //{
    //    var product = await _context.Products.FindAsync(id);

    //    if (product == null)
    //    {
    //        return NotFound();
    //    }

    //    return product;
    //}
    [HttpGet("{id}")]
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
public class CreateProductDto
{
    [Required(ErrorMessage = "Название товара обязательно для заполнения.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Название должно быть от 3 до 100 символов.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Описание не должно превышать 500 символов.")]
    public string? Description { get; set; }

    [Range(0.01, 10000000, ErrorMessage = "Цена должна быть больше нуля и не превышать 10 000 000.")]
    public decimal Price { get; set; }

    [StringLength(50, ErrorMessage = "Название сезона слишком длинное (макс. 50 символов).")]
    public string? Season { get; set; }

    [StringLength(100, ErrorMessage = "Название коллекции слишком длинное (макс. 100 символов).")]
    public string? Collection { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Укажите корректный ID категории.")]
    public int CategoryId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Укажите корректный ID бренда.")]
    public int BrandId { get; set; }

    public List<string> ImageUrls { get; set; } = new List<string>();

    public List<int> MaterialIds { get; set; } = new List<int>();
}
