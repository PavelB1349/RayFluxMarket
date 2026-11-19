using Microsoft.EntityFrameworkCore;                                                                                        
using RayFluxMarket.Data;                                                                                                   
using RayFluxMarket.Models.DTOs;                                                                                            
using RayFluxMarket.Models.Entities;                                                                                        
                                                                                                                            
namespace RayFluxMarket.Services                                                                                            
{                                                                                                                           
    public class CartService : ICartService                                                                                 
    {                                                                                                                       
        private readonly AppDbContext _context;                                                                             
                                                                                                                            
        public CartService(AppDbContext context)                                                                            
        {                                                                                                                   
            _context = context;                                                                                             
        }                                                                                                                   
                                                                                                                            
        public async Task<CartDto> GetCartAsync(int userId)                                                                 
        {                                                                                                                   
            var cartItems = await _context.CartItems
                .AsNoTracking()
                .Include(ci => ci.Product)
                    .ThenInclude(p => p!.Images)
                // Не скрываем снятые с продажи товары: иначе пользователь не сможет
                // удалить их из корзины, а CheckoutAsync будет падать с ошибкой
                .Where(ci => ci.UserId == userId && ci.Product != null)
                .ToListAsync();
                                                                                                                            
            return new CartDto                                                                                              
            {                                                                                                               
                Items = cartItems.Select(ci => new CartItemSummaryDto                                                       
                {                                                                                                           
                    ProductId = ci.ProductId,                                                                               
                    ProductName = ci.Product?.Name ?? "Товар удален",                                                       
                    Price = ci.Product?.Price ?? 0,                                                                         
                    // Главное фото выбираем по признаку IsPrimary — так же, как в каталоге и в заказах
                    MainImageUrl = ci.Product?.Images?
                        .OrderByDescending(i => i.IsPrimary)
                        .Select(i => i.Url)
                        .FirstOrDefault(),
                    Quantity = ci.Quantity                                                                                  
                }).ToList()                                                                                                 
            };                                                                                                              
        }                                                                                                                   
                                                                                                                            
        public async Task AddToCartAsync(int userId, AddToCartDto dto)                                                      
        {                                                                                                                   
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == dto.ProductId && p.IsActive);

            if (product == null)
            {
                throw new KeyNotFoundException("Товар не найден или снят с продажи.");
            }

            var existingItem = await _context.CartItems
                .FirstOrDefaultAsync(ci => ci.UserId == userId && ci.ProductId == dto.ProductId);

            int newQuantity = (existingItem?.Quantity ?? 0) + dto.Quantity;
            if (newQuantity > product.StockQuantity)
            {
                throw new InvalidOperationException(
                    $"В наличии только {product.StockQuantity} шт. товара '{product.Name}'.");
            }

            if (existingItem != null)
            {
                existingItem.Quantity = newQuantity;
            }                                                                                                               
            else                                                                                                            
            {                                                                                                               
                _context.CartItems.Add(new CartItem                                                                         
                {                                                                                                           
                    UserId = userId,                                                                                        
                    ProductId = dto.ProductId,                                                                              
                    Quantity = dto.Quantity                                                                                 
                });                                                                                                         
            }                                                                                                               
                                                                                                                            
            await _context.SaveChangesAsync();                                                                              
        }                                                                                                                   
                                                                                                                            
        public async Task UpdateQuantityAsync(int userId, AddToCartDto dto)                                                 
        {                                                                                                                   
            var cartItem = await _context.CartItems                                                                         
                .FirstOrDefaultAsync(ci => ci.UserId == userId && ci.ProductId == dto.ProductId);                           
                                                                                                                            
            if (cartItem == null)                                                                                           
            {                                                                                                               
                throw new KeyNotFoundException("Товар в корзине не найден.");                                               
            }                                                                                                               
                                                                                                                            
            if (dto.Quantity <= 0)
            {
                _context.CartItems.Remove(cartItem);
            }
            else
            {
                var product = await _context.Products.FindAsync(dto.ProductId);
                if (product == null || !product.IsActive)
                {
                    throw new InvalidOperationException("Товар недоступен для покупки.");
                }

                if (dto.Quantity > product.StockQuantity)
                {
                    throw new InvalidOperationException(
                        $"В наличии только {product.StockQuantity} шт. товара '{product.Name}'.");
                }

                cartItem.Quantity = dto.Quantity;
            }
                                                                                                                            
            await _context.SaveChangesAsync();                                                                              
        }                                                                                                                   
                                                                                                                            
        public async Task RemoveFromCartAsync(int userId, int productId)                                                    
        {                                                                                                                   
            var cartItem = await _context.CartItems                                                                         
                .FirstOrDefaultAsync(ci => ci.UserId == userId && ci.ProductId == productId);                               
                                                                                                                            
            if (cartItem == null)                                                                                           
            {                                                                                                               
                throw new KeyNotFoundException("Товар в корзине не найден.");                                               
            }                                                                                                               
                                                                                                                            
            _context.CartItems.Remove(cartItem);                                                                            
            await _context.SaveChangesAsync();                                                                              
        }                                                                                                                   
                                                                                                                            
        public async Task ClearCartAsync(int userId)                                                                        
        {                                                                                                                   
            var userItems = _context.CartItems.Where(ci => ci.UserId == userId);                                            
            _context.CartItems.RemoveRange(userItems);                                                                      
            await _context.SaveChangesAsync();                                                                              
        }                                                                                                                   
    }                                                                                                                       
}                                 
