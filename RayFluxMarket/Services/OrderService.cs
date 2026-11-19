using Microsoft.EntityFrameworkCore;                                                                                        
using Microsoft.Extensions.Caching.Memory;                                                                                  
using RayFluxMarket.Data;                                                                                                   
using RayFluxMarket.Models.DTOs;                                                                                            
using RayFluxMarket.Models.Entities;                                                                                        
using RayFluxMarket.Models.Enums;                                                                                           
                                                                                                                            
namespace RayFluxMarket.Services                                                                                            
{                                                                                                                           
    public class OrderService : IOrderService                                                                               
    {                                                                                                                       
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly IEmailService _emailService;
        private readonly IPaymentService _paymentService;

        public OrderService(AppDbContext context, IMemoryCache cache, IEmailService emailService, IPaymentService paymentService)
        {
            _context = context;
            _cache = cache;
            _emailService = emailService;
            _paymentService = paymentService;
        }
                                                                                                                            
        public async Task<OrderDto> CheckoutAsync(int userId, CheckoutDto dto)                                              
        {                                                                                                                   
            // 1. Достаем элементы корзины пользователя вместе с данными о товарах                                          
            var cartItems = await _context.CartItems                                                                        
                .Include(ci => ci.Product)                                                                                  
                .Where(ci => ci.UserId == userId)                                                                           
                .ToListAsync();                                                                                             
                                                                                                                            
            if (!cartItems.Any())                                                                                           
            {                                                                                                               
                throw new InvalidOperationException("Нельзя оформить заказ: корзина пуста.");                               
            }                                                                                                               
                                                                                                                            
            // 2. Создаем сам заказ (Шапку)                                                                                 
            var order = new Order                                                                                           
            {                                                                                                               
                UserId = userId,                                                                                            
                OrderDate = DateTime.UtcNow,                                                                                
                Status = OrderStatus.New,                                                                                   
                TotalAmount = 0,                                                                                            
                CustomerName = dto.CustomerName,                                                                            
                PhoneNumber = dto.PhoneNumber,                                                                              
                ShippingAddress = dto.ShippingAddress,                                                                      
                Comment = dto.Comment                                                                                       
            };                                                                                                              
                                                                                                                            
            // 3. Переносим товары из корзины в позиции заказа и списываем со склада                                        
            foreach (var cartItem in cartItems)                                                                             
            {                                                                                                               
                if (cartItem.Product == null) continue;

                if (!cartItem.Product.IsActive)
                {
                    throw new InvalidOperationException(
                        $"Нельзя оформить заказ: товар '{cartItem.Product.Name}' снят с продажи.");
                }

                if (cartItem.Product.StockQuantity < cartItem.Quantity)
                {                                                                                                           
                    throw new InvalidOperationException(                                                                    
                        $"Нельзя оформить заказ: недостаточно товара '{cartItem.Product.Name}' на складе. " +               
                        $"В наличии: {cartItem.Product.StockQuantity}, запрошено: {cartItem.Quantity}.");                   
                }                                                                                                           
                                                                                                                            
                cartItem.Product.StockQuantity -= cartItem.Quantity;                                                        
                                                                                                                            
                var orderItem = new OrderItem
                {
                    ProductId = cartItem.ProductId,
                    Product = cartItem.Product, // чтобы в письме было название товара
                    Quantity = cartItem.Quantity,
                    Price = cartItem.Product.Price
                };
                                                                                                                            
                order.OrderItems.Add(orderItem);                                                                            
                order.TotalAmount += orderItem.Price * orderItem.Quantity;                                                  
            }                                                                                                               
                                                                                                                            
            // 4. Сохраняем заказ и очищаем корзину в одной транзакции                                                                                        
            await using var transaction = await _context.Database.BeginTransactionAsync();                                                                    
            try                                                                                                                                               
            {                                                                                                                                                 
                _context.Orders.Add(order);                                                                                                                   
                _context.CartItems.RemoveRange(cartItems);                                                                                                    
                await _context.SaveChangesAsync();                                                                                                            
                await transaction.CommitAsync();                                                                                                              
            }                                                                                                                                                 
            catch                                                                                                                                             
            {                                                                                                                                                 
                await transaction.RollbackAsync();                                                                                                            
                throw;                                                                                                                                        
            }                                                                                         
                                                                                                                            
            // 5. Сбрасываем кэш (цены/остатки могли измениться)
            if (_cache is MemoryCache memoryCache)
            {
                memoryCache.Compact(1.0);
            }

            // 6. Отправляем письмо-подтверждение (не ломаем заказ, если почта недоступна)
            var user = await _context.Users.FindAsync(userId);
            if (user != null && !string.IsNullOrEmpty(user.Email))
            {
                try
                {
                    await _emailService.SendOrderConfirmationAsync(order, user);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Ошибка при отправке подтверждения заказа #{order.Id}: {ex.Message}");
                }
            }

            return MapToDto(order);
        }
                                                                                                                            
        public async Task<IEnumerable<OrderDto>> GetMyOrdersAsync(int userId)                                               
        {                                                                                                                   
            var orders = await _context.Orders  
                .AsNoTracking() 
                .Include(o => o.OrderItems)                                                                                 
                    .ThenInclude(oi => oi.Product)                                                                          
                        .ThenInclude(p => p!.Images)                                                                        
                .Where(o => o.UserId == userId)                                                                             
                .OrderByDescending(o => o.OrderDate)                                                                        
                .ToListAsync();                                                                                             
                                                                                                                            
            return orders.Select(MapToDto).ToList();                                                                        
        }                                                                                                                   
                                                                                                                            
        public async Task<OrderDto> GetOrderByIdAsync(int userId, int orderId, bool isAdmin)                                
        {                                                                                                                   
            var query = _context.Orders 
                .AsNoTracking() 
                .Include(o => o.OrderItems)                                                                                 
                    .ThenInclude(oi => oi.Product)                                                                          
                        .ThenInclude(p => p!.Images)                                                                        
                .AsQueryable();                                                                                             
                                                                                                                            
            if (!isAdmin)                                                                                                   
            {                                                                                                               
                query = query.Where(o => o.UserId == userId);                                                               
            }                                                                                                               
                                                                                                                            
            var order = await query.FirstOrDefaultAsync(o => o.Id == orderId);                                              
                                                                                                                            
            if (order == null)                                                                                              
            {                                                                                                               
                throw new KeyNotFoundException($"Заказ с ID {orderId} не найден.");                                         
            }                                                                                                               
                                                                                                                            
            return MapToDto(order);                                                                                         
        }                                                                                                                   
                                                                                                                            
        public async Task UpdateStatusAsync(int orderId, UpdateOrderStatusDto dto)                                          
        {                                                                                                                   
            var order = await _context.Orders.FindAsync(orderId);                                                           
                                                                                                                            
            if (order == null)                                                                                              
            {                                                                                                               
                throw new KeyNotFoundException($"Заказ с ID {orderId} не найден.");                                         
            }                                                                                                               
                                                                                                                            
            if (!OrderStatus.CanTransition(order.Status, dto.Status))
            {
                throw new InvalidOperationException(
                    $"Недопустимый переход статуса: '{order.Status}' → '{dto.Status}'.");
            }

            // Отмена заказа возвращает зарезервированный товар на склад
            if (dto.Status == OrderStatus.Cancelled)
            {
                var orderItems = await _context.OrderItems
                    .Where(oi => oi.OrderId == order.Id)
                    .ToListAsync();

                foreach (var item in orderItems)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product != null)
                    {
                        product.StockQuantity += item.Quantity;
                    }
                }

                // Если заказ уже был оплачен — возвращаем деньги через Stripe
                if (order.Status == OrderStatus.Paid)
                {
                    await _paymentService.RefundOrderAsync(order.Id);
                }
            }

            order.Status = dto.Status;
            await _context.SaveChangesAsync();

            // Остатки могли измениться — сбрасываем кэш товаров
            if (_cache is MemoryCache memoryCache)
            {
                memoryCache.Compact(1.0);
            }
        }
                                                                                                                            
        public async Task<IEnumerable<OrderDto>> GetAllOrdersAsync(string? status)                                          
        {                                                                                                                   
            var query = _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p!.Images)
                .AsNoTracking()
                .AsQueryable();
                                                                                                                            
            if (!string.IsNullOrWhiteSpace(status))                                                                         
            {                                                                                                               
                query = query.Where(o => o.Status == status);                                                               
            }                                                                                                               
                                                                                                                            
            var orders = await query                                                                                        
                .OrderByDescending(o => o.OrderDate)                                                                        
                .ToListAsync();                                                                                             
                                                                                                                            
            return orders.Select(MapToDto).ToList();                                                                        
        }                                                                                                                   
                                                                                                                            
        private static OrderDto MapToDto(Order order)                                                                       
        {                                                                                                                   
            return new OrderDto                                                                                             
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
                    ProductImageUrl = oi.Product?.Images
                        .OrderByDescending(i => i.IsPrimary)
                        .Select(i => i.Url)
                        .FirstOrDefault(),
                    Quantity = oi.Quantity,                                                                                 
                    Price = oi.Price                                                                                        
                }).ToList()                                                                                                 
            };                                                                                                              
        }                                                                                                                   
    }                                                                                                                       
}                                         
