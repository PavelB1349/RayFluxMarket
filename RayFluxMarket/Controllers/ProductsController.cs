using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RayFluxMarket.Models.Entities;
using RayFluxMarket.Data;

[Route("api/[controller]")]
[ApiController]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _context;
    public ProductsController(AppDbContext context)
    {
        _context = context;
    }

    // GET: api/Product
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Product>>> GetProduct()
    {
        //return await _context.Products.ToListAsync();
        return await _context.Products
        .Include(p => p.Images)      // Подтягиваем все фото
        .Include(p => p.Materials)   // Подтягиваем материалы
        .Include(p => p.Brand)       // Подтягиваем бренд (название, лого и т.д.)
        .Include(p => p.Category)    // Подтягиваем категорию
        .AsNoTracking() // Для оптимизации чтения, если не планируем изменять эти объекты
        .ToListAsync();
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
    [HttpPut("{id}")]
    public async Task<IActionResult> PutProduct(int? id, Product product)
    {
        if (id != product.Id)
        {
            return BadRequest();
        }

        _context.Entry(product).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!ProductExists(id))
            {
                return NotFound();
            }
            else
            {
                throw;
            }
        }

        return NoContent();
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
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProduct(int? id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null)
        {
            return NotFound();
        }

        _context.Products.Remove(product);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private bool ProductExists(int? id)
    {
        return _context.Products.Any(e => e.Id == id);
    }
}
public class CreateProductDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? Season { get; set; }
    public string? Collection { get; set; }
    public int CategoryId { get; set; }
    public int BrandId { get; set; }

    // Список URL-адресов картинок
    public List<string> ImageUrls { get; set; } = new List<string>();

    // Список ID материалов
    public List<int> MaterialIds { get; set; } = new List<int>();
}
