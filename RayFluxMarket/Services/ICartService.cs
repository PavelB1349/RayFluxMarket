using RayFluxMarket.Models.DTOs;                                                                                            
                                                                                                                            
namespace RayFluxMarket.Services                                                                                            
{                                                                                                                           
    public interface ICartService                                                                                           
    {                                                                                                                       
        Task<CartDto> GetCartAsync(int userId);                                                                             
        Task AddToCartAsync(int userId, AddToCartDto dto);                                                                  
        Task UpdateQuantityAsync(int userId, AddToCartDto dto);                                                             
        Task RemoveFromCartAsync(int userId, int productId);                                                                
        Task ClearCartAsync(int userId);                                                                                    
    }                                                                                                                       
}