using RayFluxMarket.Models.DTOs;                                                                                            
                                                                                                                            
namespace RayFluxMarket.Services                                                                                            
{                                                                                                                           
    public interface IOrderService                                                                                          
    {                                                                                                                       
        Task<OrderDto> CheckoutAsync(int userId, CheckoutDto dto);                                                          
        Task<IEnumerable<OrderDto>> GetMyOrdersAsync(int userId);                                                           
        Task<OrderDto> GetOrderByIdAsync(int userId, int orderId, bool isAdmin);                                            
        Task UpdateStatusAsync(int orderId, UpdateOrderStatusDto dto);                                                      
        Task<IEnumerable<OrderDto>> GetAllOrdersAsync(string? status);                                                      
    }                                                                                                                       
}