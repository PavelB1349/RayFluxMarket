using Microsoft.AspNetCore.Authorization;                                                                                                                    
using Microsoft.AspNetCore.Mvc;                                                                                                                               
using RayFluxMarket.Extensions;                                                                                                                               
using RayFluxMarket.Models.DTOs;                                                                                                                              
using RayFluxMarket.Services;                                                                                                
                                                                                                                            
namespace RayFluxMarket.Controllers                                                                                         
{                                                                                                                           
    [Route("api/[controller]")]                                                                                             
    [ApiController]                                                                                                         
    [Authorize]                                                                                                             
    public class OrdersController : ControllerBase                                                                          
    {                                                                                                                       
        private readonly IOrderService _orderService;                                                                       
                                                                                                                            
        public OrdersController(IOrderService orderService)                                                                 
        {                                                                                                                   
            _orderService = orderService;                                                                                   
        }    

        // 1. POST: api/Orders/Checkout (Оформить заказ из корзины)                                                         
        [HttpPost("Checkout")]                                                                                              
        public async Task<IActionResult> Checkout([FromBody] CheckoutDto dto)                                               
        {                                                                                                                   
            int userId = User.GetUserId();                                                                                
            var order = await _orderService.CheckoutAsync(userId, dto);                                                     
                                                                                                                            
            return CreatedAtAction(nameof(GetOrderById), new { id = order.Id },                                             
                new { message = "Заказ успешно оформлен!", orderId = order.Id });                                           
        }       

       

        // 2. GET: api/Orders/MyOrders (История заказов текущего пользователя)                                              
        [HttpGet("MyOrders")]                                                                                               
        public async Task<ActionResult<IEnumerable<OrderDto>>> GetMyOrders()                                                
        {                                                                                                                   
            int userId = User.GetUserId();                                                                              
                                                                                       
            var orders = await _orderService.GetMyOrdersAsync(userId);                                                      
            return Ok(orders);                                                                                              
        }    

        // 3. GET: api/Orders/5 (Получить конкретный заказ по ID)                                                           
        [HttpGet("{id}")]                                                                                                   
        public async Task<ActionResult<OrderDto>> GetOrderById(int id)                                                      
        {                                                                                                                   
            int userId = User.GetUserId();                                                                                
            bool isAdmin = User.IsInRole("Admin");                                                                          
            var order = await _orderService.GetOrderByIdAsync(userId, id, isAdmin);                                         
            return Ok(order);                                                                                               
        }     

        // 4. PUT: api/Orders/{id}/status (Смена статуса заказа — ТОЛЬКО ДЛЯ АДМИНА)                                        
        [HttpPut("{id}/status")]                                                                                            
        [Authorize(Roles = "Admin")] // Эндпоинт доступен исключительно администраторам!                                    
        public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusDto dto)                     
        {                                                                                                                   
            await _orderService.UpdateStatusAsync(id, dto);                                                                 
                                                                                                                            
            return Ok(new { message = $"Статус заказа №{id} успешно изменен на '{dto.Status}'.", orderId = id, newStatus =  
                dto.Status });                                                                                                              
        }  

        // 5. GET: api/Orders/admin/all (Получить ВСЕ заказы в системе — ТОЛЬКО ДЛЯ АДМИНА)                                 
        [HttpGet("admin/all")]                                                                                              
        [Authorize(Roles = "Admin")]                                                                                        
        public async Task<ActionResult<IEnumerable<OrderDto>>> GetAllOrders([FromQuery] string? status)                     
        {                                                                                                                   
            var orders = await _orderService.GetAllOrdersAsync(status);                                                     
            return Ok(orders);                                                                                              
        }      
       
    }
}
