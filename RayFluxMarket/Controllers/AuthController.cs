using Microsoft.AspNetCore.Mvc;                                                                                            
using RayFluxMarket.Models.DTOs;                                                                                            
using RayFluxMarket.Services;                                                                                               
                                                                                                                            
namespace RayFluxMarket.Controllers                                                                                         
{                                                                                                                           
    [Route("api/[controller]")]                                                                                             
    [ApiController]                                                                                                         
    public class AuthController : ControllerBase                                                                            
    {                                                                                                                       
        private readonly IAuthService _authService;                                                                         
                                                                                                                            
        public AuthController(IAuthService authService)                                                                     
        {                                                                                                                   
            _authService = authService;                                                                                     
        } 

       // 1. POST: api/Auth/Register (Регистрация нового пользователя)                                                     
        [HttpPost("Register")]                                                                                              
        public async Task<IActionResult> Register(RegisterDto dto)                                                          
        {                                                                                                                   
            var result = await _authService.RegisterAsync(dto);                                                             
            if (!result.Success)                                                                                            
            {                                                                                                               
                return BadRequest(new { message = result.ErrorMessage });                                                   
            }                                                                                                               
                                                                                                                            
            return Ok(new                                                                                                   
            {                                                                                                               
                token = result.AccessToken,                                                                                 
                accessToken = result.AccessToken,                                                                           
                refreshToken = result.RefreshToken,                                                                         
                user = new                                                                                                  
                {                                                                                                           
                    id = result.User!.Id,                                                                                   
                    email = result.User.Email                                                                               
                }                                                                                                           
            });                                                                                                             
        }           

        // 2. POST: api/Auth/Login (Вход в систему)                                                                         
        [HttpPost("Login")]                                                                                                 
        public async Task<IActionResult> Login(LoginDto dto)                                                                
        {                                                                                                                   
            var result = await _authService.LoginAsync(dto);                                                                
            if (!result.Success)                                                                                            
            {                                                                                                               
                return BadRequest(new { message = result.ErrorMessage });                                                   
            }                                                                                                               
                                                                                                                            
            return Ok(new                                                                                                   
            {                                                                                                               
                message = "Успешный вход!",                                                                                 
                token = result.AccessToken,                                                                                 
                accessToken = result.AccessToken,                                                                           
                refreshToken = result.RefreshToken,                                                                         
                user = new                                                                                                  
                {                                                                                                           
                    id = result.User!.Id,                                                                                   
                    email = result.User.Email                                                                               
                }                                                                                                           
            });                                                                                                             
        }   

        // 3. POST: api/Auth/refresh (Обновление пары токенов)                                                              
        [HttpPost("refresh")]                                                                                               
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto dto)                                       
        {                                                                                                                   
            var result = await _authService.RefreshTokenAsync(dto);                                                         
            if (!result.Success)                                                                                            
            {                                                                                                               
                return Unauthorized(new { message = result.ErrorMessage });                                                 
            }                                                                                                               
                                                                                                                            
            return Ok(new                                                                                                   
            {                                                                                                               
                token = result.AccessToken,                                                                                 
                accessToken = result.AccessToken,                                                                           
                refreshToken = result.RefreshToken                                                                          
            });                                                                                                             
        }    

        // 4. POST: api/Auth/forgot-password (Запрос на сброс пароля)                                                       
        [HttpPost("forgot-password")]                                                                                       
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)                                   
        {                                                                                                                   
            await _authService.ForgotPasswordAsync(dto);                                                                    
            return Ok(new { message = "Если такой Email зарегистрирован, мы отправили инструкцию по сбросу пароля." });     
        }    

        // 5. POST: api/Auth/reset-password (Установка нового пароля по токену)                                             
        [HttpPost("reset-password")]                                                                                        
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)                                     
        {                                                                                                                   
            var result = await _authService.ResetPasswordAsync(dto);                                                        
            if (!result.Success)                                                                                            
            {                                                                                                               
                return BadRequest(new { message = result.ErrorMessage });                                                   
            }                                                                                                               
                                                                                                                            
            return Ok(new { message = "Пароль успешно изменен! Теперь вы можете войти с новым паролем." });                 
        }    

        
    }
}