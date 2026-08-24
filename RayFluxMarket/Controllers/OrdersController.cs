using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RayFluxMarket.Data;
using RayFluxMarket.Models.DTOs;
using RayFluxMarket.Models.Entities;
using RayFluxMarket.Models.Enums;
using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;

namespace RayFluxMarket.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly AppDbContext _context;
        //private const int MockUserId = 1; // Временная заглушка пользователя
        private readonly IMemoryCache _cache;

        public OrdersController(AppDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        // 1. POST: api/Orders/Checkout (Оформить заказ из корзины)
        [HttpPost("Checkout")]
        public async Task<IActionResult> Checkout()
        {
            int userId = GetCurrentUserId();
            // 1. Достаем элементы корзины пользователя вместе с данными о товарах
            var cartItems = await _context.CartItems
                .Include(ci => ci.Product)
                .Where(ci => ci.UserId == userId)
                .ToListAsync();

            if (!cartItems.Any())
            {
                return BadRequest(new { message = "Нельзя оформить заказ: корзина пуста." });
            }

            // 2. Создаем сам заказ (Шапку)
            var order = new Order
            {
                UserId = userId,
                OrderDate = DateTime.UtcNow,
                Status = OrderStatus.New,
                TotalAmount = 0 // Посчитаем ниже
            };

            // 3. Переносим товары из корзины в позиции заказа и списываем со склада
            foreach (var cartItem in cartItems)
            {
                if (cartItem.Product == null) continue;// На всякий случай, если товар удалили из каталога

                // --- НОВАЯ ЛОГИКА: ПРОВЕРКА СКЛАДА ---
                if (cartItem.Product.StockQuantity < cartItem.Quantity)
                {
                    return BadRequest(new
                    {
                        message = $"Нельзя оформить заказ: недостаточно товара '{cartItem.Product.Name}' на складе. В наличии: {cartItem.Product.StockQuantity}, запрошено: {cartItem.Quantity}."
                    });
                }

                // Списываем купленное количество товара со склада
                cartItem.Product.StockQuantity -= cartItem.Quantity;
                // -------------------------------------

                var orderItem = new OrderItem
                {
                    ProductId = cartItem.ProductId,
                    Quantity = cartItem.Quantity,
                    Price = cartItem.Product.Price // Фиксируем цену на момент покупки!
                };

                order.OrderItems.Add(orderItem);
                order.TotalAmount += orderItem.Price * orderItem.Quantity;
            }

            // 4. Сохраняем заказ в базу данных
            _context.Orders.Add(order);

            // 5. Очищаем корзину пользователя
            _context.CartItems.RemoveRange(cartItems);

            // Сохраняем все изменения в базе одной транзакцией
            await _context.SaveChangesAsync();

            if (_cache is MemoryCache memoryCache)// Проверяем, что кэш действительно является MemoryCache
            {
                memoryCache.Compact(1.0); // 1.0 означает сброс кэша на 100%
            }

            return CreatedAtAction(nameof(GetOrderById), new { id = order.Id }, new { message = "Заказ успешно оформлен!", orderId = order.Id });
        }

       

        // 2. GET: api/Orders/MyOrders (История заказов текущего пользователя)
        [HttpGet("MyOrders")]
        public async Task<ActionResult<IEnumerable<OrderDto>>> GetMyOrders()
        {
            int userId = GetCurrentUserId();
            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .ThenInclude(p => p.Images)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            var orderDtos = orders.Select(o => new OrderDto
            {
                Id = o.Id,
                UserId = o.UserId,
                OrderDate = o.OrderDate,
                Status = o.Status,
                TotalAmount = o.TotalAmount,
                Items = o.OrderItems.Select(oi => new OrderItemDto
                {
                    ProductId = oi.ProductId,
                    ProductName = oi.Product?.Name ?? "Товар удален из каталога",
                    ProductImageUrl = oi.Product?.Images.FirstOrDefault()?.Url,
                    Quantity = oi.Quantity,
                    Price = oi.Price
                }).ToList()
            }).ToList();

            return Ok(orderDtos);
        }

        // 3. GET: api/Orders/5 (Получить конкретный заказ по ID)
        [HttpGet("{id}")]
        public async Task<ActionResult<OrderDto>> GetOrderById(int id)
        {
            int userId = GetCurrentUserId();
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .ThenInclude(p => p.Images)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null)
            {
                return NotFound(new { message = $"Заказ с ID {id} не найден." });
            }

            var orderDto = new OrderDto
            {
                Id = order.Id,
                UserId = order.UserId,
                OrderDate = order.OrderDate,
                Status = order.Status,
                TotalAmount = order.TotalAmount,
                Items = order.OrderItems.Select(oi => new OrderItemDto
                {
                    ProductId = oi.ProductId,
                    ProductName = oi.Product?.Name ?? "Товар удален из каталога",
                    ProductImageUrl = oi.Product?.Images.FirstOrDefault()?.Url,
                    Quantity = oi.Quantity,
                    Price = oi.Price
                }).ToList()
            };

            return Ok(orderDto);
        }

        // 4. PUT: api/Orders/{id}/status (Смена статуса заказа — ТОЛЬКО ДЛЯ АДМИНА)
        [HttpPut("{id}/status")]
        [Authorize(Roles = "Admin")] // Эндпоинт доступен исключительно администраторам!
        public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusDto dto)
        {
            // Ищем заказ в базе (тут мы НЕ фильтруем по userId, так как админ имеет право видеть и менять любые заказы!)
            var order = await _context.Orders.FindAsync(id);

            if (order == null)
            {
                return NotFound(new { message = $"Заказ с ID {id} не найден." });
            }

           
            // Меняем статус и сохраняем
            order.Status = dto.Status;
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Статус заказа №{id} успешно изменен на '{dto.Status}'.", orderId = id, newStatus = order.Status });
        }

        // 5. GET: api/Orders/admin/all (Получить ВСЕ заказы в системе — ТОЛЬКО ДЛЯ АДМИНА)
        [HttpGet("admin/all")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<OrderDto>>> GetAllOrders([FromQuery] string? status)
        {
            var query = _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .AsNoTracking()
                .AsQueryable();

            // Если передали параметр фильтрации (например ?status=Paid), фильтруем
            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(o => o.Status == status);
            }

            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            var orderDtos = orders.Select(o => new OrderDto
            {
                Id = o.Id,
                UserId = o.UserId,
                OrderDate = o.OrderDate,
                Status = o.Status,
                TotalAmount = o.TotalAmount,
                Items = o.OrderItems.Select(oi => new OrderItemDto
                {
                    ProductId = oi.ProductId,
                    ProductName = oi.Product?.Name ?? "Товар удален из каталога",
                    Quantity = oi.Quantity,
                    Price = oi.Price
                }).ToList()
            }).ToList();

            return Ok(orderDtos);
        }


        private int GetCurrentUserId()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr))
            {
                throw new UnauthorizedAccessException("ID пользователя не найден в токене.");
            }
            return int.Parse(userIdStr);
        }
    }
}
