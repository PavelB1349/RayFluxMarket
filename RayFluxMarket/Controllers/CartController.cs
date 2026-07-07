using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RayFluxMarket.Data;
using RayFluxMarket.Models.DTOs;
using RayFluxMarket.Models.Entities;
using System.Security.Claims; // Нужен для чтения Claims из токена


[Route("api/[controller]")]
[ApiController]
[Authorize]
public class CartController : ControllerBase
{
    private readonly AppDbContext _context; 

    

    public CartController(AppDbContext context)
    {
        _context = context;
    }

    // 1. GET: api/Cart
    [HttpGet]
    public async Task<ActionResult<CartDto>> GetCart()
    {
        int userId = GetCurrentUserId(); // Вытаскиваем реальный ID из токена

        var cartItems = await _context.CartItems
            .Include(ci => ci.Product)
                .ThenInclude(p => p.Images)
            .Where(ci => ci.UserId == userId) // Используем реальный ID
            .ToListAsync();

        var cartDto = new CartDto
        {
            Items = cartItems.Select(ci => new CartItemSummaryDto
            {
                ProductId = ci.ProductId,
                ProductName = ci.Product?.Name ?? "Товар удален",
                Price = ci.Product?.Price ?? 0,
                MainImageUrl = ci.Product?.Images != null && ci.Product.Images.Any()
                    ? ci.Product.Images.FirstOrDefault()?.Url
                    : null,
                Quantity = ci.Quantity
            }).ToList()
        };

        return Ok(cartDto);
    }

    // 2. POST: api/Cart/Add
    [HttpPost("Add")]
    public async Task<IActionResult> AddToCart(AddToCartDto dto)
    {
        if (dto.Quantity <= 0)
        {
            return BadRequest(new { message = "Количество товара должно быть больше нуля." });
        }

        int userId = GetCurrentUserId();

        var product = await _context.Products.FindAsync(dto.ProductId);
        if (product == null)
        {
            return NotFound(new { message = "Товар не найден." });
        }

        var existingItem = await _context.CartItems
            .FirstOrDefaultAsync(ci => ci.UserId == userId && ci.ProductId == dto.ProductId);

        if (existingItem != null)
        {
            existingItem.Quantity += dto.Quantity;
        }
        else
        {
            var newItem = new CartItem
            {
                UserId = userId,
                ProductId = dto.ProductId,
                Quantity = dto.Quantity
            };
            _context.CartItems.Add(newItem);
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Товар успешно добавлен в корзину." });
    }

    // 3. PUT: api/Cart/UpdateQuantity
    [HttpPut("UpdateQuantity")]
    public async Task<IActionResult> UpdateQuantity(AddToCartDto dto)
    {
        int userId = GetCurrentUserId();

        var cartItem = await _context.CartItems
            .FirstOrDefaultAsync(ci => ci.UserId == userId && ci.ProductId == dto.ProductId);

        if (cartItem == null)
        {
            return NotFound(new { message = "Товар в корзине не найден." });
        }

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

    // 4. DELETE: api/Cart/Remove/{productId}
    [HttpDelete("Remove/{productId}")]
    public async Task<IActionResult> RemoveFromCart(int productId)
    {
        int userId = GetCurrentUserId();

        var cartItem = await _context.CartItems
            .FirstOrDefaultAsync(ci => ci.UserId == userId && ci.ProductId == productId);

        if (cartItem == null)
        {
            return NotFound(new { message = "Товар в корзине не найден." });
        }

        _context.CartItems.Remove(cartItem);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Товар удален из корзины." });
    }

    // 5. DELETE: api/Cart/Clear
    [HttpDelete("Clear")]
    public async Task<IActionResult> ClearCart()
    {
        int userId = GetCurrentUserId();

        var userItems = _context.CartItems.Where(ci => ci.UserId == userId);

        _context.CartItems.RemoveRange(userItems);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Корзина полностью очищена." });
    }

    // СЕКРЕТНЫЙ МЕТОД: Вытаскивает ID из "паспорта" (токена) текущего запроса
    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
        {
            throw new UnauthorizedAccessException("Пользователь не авторизован.");
        }
        return int.Parse(userIdClaim.Value);
    }
}
