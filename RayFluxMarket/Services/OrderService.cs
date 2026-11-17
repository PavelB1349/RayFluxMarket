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
                                                                                                                            
        public OrderService(AppDbContext context, IMemoryCache cache)                                                       
        {                                                                                                                   
            _context = context;                                                                                             
            _cache = cache;                                                                                                 
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
                    Quantity = cartItem.Quantity,                                                                           
                    Price = cartItem.Product.Price                                                                          
                };                                                                                                          
                                                                                                                            
                order.OrderItems.Add(orderItem);                                                                            
                order.TotalAmount += orderItem.Price * orderItem.Quantity;                                                  
            }                                                                                                               
                                                                                                                            
            // 4. Сохраняем заказ и очищаем корзину                                                                         
            _context.Orders.Add(order);                                                                                     
            _context.CartItems.RemoveRange(cartItems);                                                                      
            await _context.SaveChangesAsync();                                                                              
                                                                                                                            
            // 5. Сбрасываем кэш (цены/остатки могли измениться)                                                            
            if (_cache is MemoryCache memoryCache)                                                                          
            {                                                                                                               
                memoryCache.Compact(1.0);                                                                                   
            }                                                                                                               
                                                                                                                            
            return MapToDto(order);                                                                                         
        }                                                                                                                   
                                                                                                                            
        public async Task<IEnumerable<OrderDto>> GetMyOrdersAsync(int userId)                                               
        {                                                                                                                   
            var orders = await _context.Orders                                                                              
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
                                                                                                                            
            order.Status = dto.Status;                                                                                      
            await _context.SaveChangesAsync();                                                                              
        }                                                                                                                   
                                                                                                                            
        public async Task<IEnumerable<OrderDto>> GetAllOrdersAsync(string? status)                                          
        {                                                                                                                   
            var query = _context.Orders                                                                                     
                .Include(o => o.OrderItems)                                                                                 
                    .ThenInclude(oi => oi.Product)                                                                          
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
                    ProductImageUrl = oi.Product?.Images.FirstOrDefault()?.Url,                                             
                    Quantity = oi.Quantity,                                                                                 
                    Price = oi.Price                                                                                        
                }).ToList()                                                                                                 
            };                                                                                                              
        }                                                                                                                   
    }                                                                                                                       
}                                         