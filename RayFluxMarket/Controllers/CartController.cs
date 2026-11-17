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
    public class CartController : ControllerBase
    {
        private readonly ICartService _cartService;

        public CartController(ICartService cartService)
        {
            _cartService = cartService;
        }

        // 1. GET: api/Cart                                                                                                     
        [HttpGet]
        public async Task<ActionResult<CartDto>> GetCart()
        {
            int userId = User.GetUserId();
            var cartDto = await _cartService.GetCartAsync(userId);
            return Ok(cartDto);
        }

        // 2. POST: api/Cart/Add                                                                                                
        [HttpPost("Add")]
        public async Task<IActionResult> AddToCart(AddToCartDto dto)
        {
            int userId = User.GetUserId();
            await _cartService.AddToCartAsync(userId, dto);
            return Ok(new { message = "Товар успешно добавлен в корзину." });
        }

        // 3. PUT: api/Cart/UpdateQuantity                                                                                      
        [HttpPut("UpdateQuantity")]
        public async Task<IActionResult> UpdateQuantity(AddToCartDto dto)
        {
            int userId = User.GetUserId();
            await _cartService.UpdateQuantityAsync(userId, dto);

            var message = dto.Quantity <= 0
                ? "Товар удален из корзины."
                : "Количество товара обновлено.";

            return Ok(new { message });
        }

        // 4. DELETE: api/Cart/Remove/{productId}                                                                               
        [HttpDelete("Remove/{productId}")]
        public async Task<IActionResult> RemoveFromCart(int productId)
        {
            int userId = User.GetUserId();
            await _cartService.RemoveFromCartAsync(userId, productId);
            return Ok(new { message = "Товар удален из корзины." });
        }

        // 5. DELETE: api/Cart/Clear                                                                                            
        [HttpDelete("Clear")]
        public async Task<IActionResult> ClearCart()
        {
            int userId = User.GetUserId();
            await _cartService.ClearCartAsync(userId);
            return Ok(new { message = "Корзина полностью очищена." });
        }
        
    }
}
