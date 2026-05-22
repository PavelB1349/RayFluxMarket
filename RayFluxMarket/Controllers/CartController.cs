using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RayFluxMarket.Data;
using RayFluxMarket.Models.DTOs;
using RayFluxMarket.Models.Entities;


[Route("api/[controller]")]
[ApiController]
public class CartController : ControllerBase
{
    private readonly AppDbContext _context; 

    // Временная заглушка для ID пользователя, пока нет авторизации
    private const int MockUserId = 1;

    public CartController(AppDbContext context)
    {
        _context = context;
    }

    // 1. GET: api/Cart (Получить корзину текущего пользователя)
    [HttpGet]
    public async Task<ActionResult<CartDto>> GetCart()
    {
        // Достаем из базы элементы корзины для нашего пользователя и сразу подтягиваем данные о самом товаре (через Include)
        var cartItems = await _context.CartItems
            .Include(ci => ci.Product)
                .ThenInclude(p => p.Images) // если товар есть, то подтягиваем его фото, чтобы показать в корзине
            .Where(ci => ci.UserId == MockUserId)
            .ToListAsync();

        // Маппим (перекладываем) данные из сущностей базы в наш CartDto
        var cartDto = new CartDto
        {
            Items = cartItems.Select(ci => new CartItemSummaryDto
            {
                ProductId = ci.ProductId,
                ProductName = ci.Product?.Name ?? "Товар удален",
                Price = ci.Product?.Price ?? 0,
                MainImageUrl = ci.Product?.Images != null && ci.Product.Images.Any() // волшебство null-условного оператора, чтобы не упасть, если товар удален или нет фото
                ? ci.Product.Images.FirstOrDefault()?.Url  : null, // хрен поймешь, может быть null, если товар удален или нет фото
                Quantity = ci.Quantity
            }).ToList()
        };

        return Ok(cartDto);
    }

    // 2. POST: api/Cart/Add (Добавить товар в корзину)
    [HttpPost("Add")]
    public async Task<IActionResult> AddToCart(AddToCartDto dto)
    {
        // Проверяем, существует ли вообще такой товар в каталоге
        var product = await _context.Products.FindAsync(dto.ProductId);
        if (product == null)
        {
            return NotFound(new { message = "Товар не найден." });
        }

        // Проверяем, нет ли уже такого товара в корзине этого пользователя
        var existingItem = await _context.CartItems
            .FirstOrDefaultAsync(ci => ci.UserId == MockUserId && ci.ProductId == dto.ProductId);

        if (existingItem != null)
        {
            // Если товар уже есть — просто увеличиваем его количество
            existingItem.Quantity += dto.Quantity;
        }
        else
        {
            // Если товара нет — создаем новую запись в корзине
            var newItem = new CartItem
            {
                UserId = MockUserId,
                ProductId = dto.ProductId,
                Quantity = dto.Quantity
            };
            _context.CartItems.Add(newItem);
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Товар успешно добавлен в корзину." });
    }

    // 3. PUT: api/Cart/UpdateQuantity (Изменить количество товара напрямую, например, кнопками + и -)
    [HttpPut("UpdateQuantity")]
    public async Task<IActionResult> UpdateQuantity(AddToCartDto dto)
    {
        var cartItem = await _context.CartItems
            .FirstOrDefaultAsync(ci => ci.UserId == MockUserId && ci.ProductId == dto.ProductId);

        if (cartItem == null)
        {
            return NotFound(new { message = "Товар в корзине не найден." });
        }

        // Если количество скрутили в 0 или меньше — удаляем товар из корзины
        if (dto.Quantity <= 0)
        {
            _context.CartItems.Remove(cartItem);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Товар удален из корзины." });
        }

        cartItem.Quantity = dto.Quantity;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Количество товара обновлено." });
    }

    // 4. DELETE: api/Cart/Remove/{productId} (Удалить конкретный товар из корзины полностью)
    [HttpDelete("Remove/{productId}")]
    public async Task<IActionResult> RemoveFromCart(int productId)
    {
        var cartItem = await _context.CartItems
            .FirstOrDefaultAsync(ci => ci.UserId == MockUserId && ci.ProductId == productId);

        if (cartItem == null)
        {
            return NotFound(new { message = "Товар в корзине не найден." });
        }

        _context.CartItems.Remove(cartItem);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Товар удален из корзины." });
    }

    // 5. DELETE: api/Cart/Clear (Полностью очистить корзину — пригодится при оформлении заказа)
    [HttpDelete("Clear")]
    public async Task<IActionResult> ClearCart()
    {
        var userItems = _context.CartItems.Where(ci => ci.UserId == MockUserId);

        _context.CartItems.RemoveRange(userItems);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Корзина полностью очищена." });
    }
}
