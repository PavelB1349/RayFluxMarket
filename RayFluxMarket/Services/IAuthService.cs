using RayFluxMarket.Models.DTOs;                                                                                            
using RayFluxMarket.Models.Entities;                                                                                        

namespace RayFluxMarket.Services;

 // Результат работы AuthService — не привязан к HTTP, чтобы сервис можно было тестировать отдельно                      
    public class AuthResult                                                                                                 
    {                                                                                                                       
        public bool Success { get; init; }                                                                                  
        public string? ErrorMessage { get; init; }                                                                          
        public string? AccessToken { get; init; }                                                                           
        public string? RefreshToken { get; init; }                                                                          
        public User? User { get; init; }                                                                                    
                                                                                                                            
        public static AuthResult Fail(string message) => new() { Success = false, ErrorMessage = message };                 
                                                                                                                            
        public static AuthResult Ok(string accessToken, string refreshToken, User user) =>                                  
            new() { Success = true, AccessToken = accessToken, RefreshToken = refreshToken, User = user };                  
    }                                                                                                                       
                                                                                                                            
    public interface IAuthService                                                                                           
    {                                                                                                                       
        Task<AuthResult> RegisterAsync(RegisterDto dto);                                                                    
        Task<AuthResult> LoginAsync(LoginDto dto);                                                                          
        Task<AuthResult> RefreshTokenAsync(RefreshTokenDto dto);                                                            
        Task ForgotPasswordAsync(ForgotPasswordDto dto);                                                                    
        Task<AuthResult> ResetPasswordAsync(ResetPasswordDto dto);                                                          
    }            