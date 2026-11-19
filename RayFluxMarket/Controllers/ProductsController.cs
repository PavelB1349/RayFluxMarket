using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RayFluxMarket.Models.DTOs;
using RayFluxMarket.Services;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin")] // <-- По умолчанию ВСЕ методы контроллера теперь только для Админа!
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }


    // GET: api/Products
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetProducts([FromQuery] ProductQueryParameters query)
    {
        var products = await _productService.GetProductsAsync(query);
        return Ok(products);
    }


    [HttpGet("{id}")]
    [AllowAnonymous] // <-- Этот метод теперь доступен всем, даже неавторизованным пользователям
    public async Task<ActionResult<ProductDto>> GetProduct(int id)
    {
        var product = await _productService.GetProductByIdAsync(id);
        return Ok(product);
    }

    
    [HttpPut("{id}")]
    public async Task<IActionResult> PutProduct(int id, CreateProductDto dto)
    {
        await _productService.UpdateProductAsync(id, dto);
        return NoContent(); // Стандартный успешный ответ для PUT (204 No Content)
    }

    [HttpPost]
    public async Task<ActionResult<ProductDto>> PostProduct(CreateProductDto dto)
    {
        var product = await _productService.CreateProductAsync(dto);
        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
    }


    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        await _productService.DeleteProductAsync(id);
        return NoContent();
    }

    // POST: api/Products/{id}/image (Загрузка картинки к товару — ТОЛЬКО ДЛЯ АДМИНА)
    [HttpPost("{id}/image")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UploadProductImage(int id, IFormFile file)
    {
        try
        {
            var relativePath = await _productService.UploadImageAsync(id, file);
            return Ok(new { message = "Картинка успешно загружена.", url = relativePath });
        }
        catch (ArgumentException ex)
        {
            // Если сервис выкинул ошибку (например, не тот формат), отдаем 400 BadRequest
            return BadRequest(new { message = ex.Message });
        }
    }
}
