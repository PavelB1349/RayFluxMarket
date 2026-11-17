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
                .Include(ci => ci.Product)                                                                                  
                    .ThenInclude(p => p!.Images)                                                                            
                .Where(ci => ci.UserId == userId)                                                                           
                .ToListAsync();                                                                                             
                                                                                                                            
            return new CartDto                                                                                              
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
        }                                                                                                                   
                                                                                                                            
        public async Task AddToCartAsync(int userId, AddToCartDto dto)                                                      
        {                                                                                                                   
            var product = await _context.Products.FindAsync(dto.ProductId);                                                 
            if (product == null)                                                                                            
            {                                                                                                               
                throw new KeyNotFoundException("Товар не найден.");                                                         
            }                                                                                                               
                                                                                                                            
            var existingItem = await _context.CartItems                                                                     
                .FirstOrDefaultAsync(ci => ci.UserId == userId && ci.ProductId == dto.ProductId);                           
                                                                                                                            
            if (existingItem != null)                                                                                       
            {                                                                                                               
                existingItem.Quantity += dto.Quantity;                                                                      
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