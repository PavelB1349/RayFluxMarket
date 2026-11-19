using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RayFluxMarket.Configuration;
using RayFluxMarket.Data;
using RayFluxMarket.Models.DTOs;
using RayFluxMarket.Models.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace RayFluxMarket.Services
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly JwtSettings _jwtSettings;
        private readonly IEmailService _emailService;

        public AuthService(AppDbContext context, IOptions<JwtSettings> jwtOptions, IEmailService emailService)
        {
            _context = context;
            _jwtSettings = jwtOptions.Value;
            _emailService = emailService;
        }
                                                                                                                            
        public async Task<AuthResult> RegisterAsync(RegisterDto dto)                                                        
        {                                                                                                                   
            var userExists = await _context.Users.AnyAsync(u => u.Email == dto.Email);                                      
            if (userExists)                                                                                                 
            {                                                                                                               
                return AuthResult.Fail("Пользователь с таким Email уже зарегистрирован.");                                  
            }                                                                                                               
                                                                                                                            
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);                                             
                                                                                                                            
            var newUser = new User                                                                                          
            {                                                                                                               
                Email = dto.Email,                                                                                          
                PasswordHash = passwordHash,                                                                                
                Role = "User"                                                                                               
            };                                                                                                              
                                                                                                                            
            var refreshToken = GenerateRefreshToken();

            newUser.RefreshToken = refreshToken;
            newUser.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(GetRefreshTokenDays());

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // Генерируем access-token только после сохранения, иначе в JWT попадает Id = 0
            var accessToken = GenerateAccessToken(newUser);
                                                                                                                            
            try
            {
                await _emailService.SendWelcomeAsync(newUser);
            }
            catch (Exception)
            {
                // Игнорируем ошибки почты, чтобы не ломать регистрацию
            }
                                                                                                                            
            return AuthResult.Ok(accessToken, refreshToken, newUser);                                                       
        }                                                                                                                   
                                                                                                                            
        public async Task<AuthResult> LoginAsync(LoginDto dto)                                                              
        {                                                                                                                   
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);                                 
            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))                                 
            {                                                                                                               
                return AuthResult.Fail("Неверный Email или пароль.");                                                       
            }                                                                                                               
                                                                                                                            
            var accessToken = GenerateAccessToken(user);                                                                    
            var refreshToken = GenerateRefreshToken();                                                                      
                                                                                                                            
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(GetRefreshTokenDays());
            await _context.SaveChangesAsync();

            return AuthResult.Ok(accessToken, refreshToken, user);
        }
                                                                                                                            
        public async Task<AuthResult> RefreshTokenAsync(RefreshTokenDto dto)                                                
        {                                                                                                                   
            if (string.IsNullOrEmpty(dto.RefreshToken))                                                                     
            {                                                                                                               
                return AuthResult.Fail("Токен обновления не передан.");                                                     
            }                                                                                                               
                                                                                                                            
            var user = await _context.Users.FirstOrDefaultAsync(u => u.RefreshToken == dto.RefreshToken);                   
                                                                                                                            
            if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)                                             
            {                                                                                                               
                return AuthResult.Fail("Недействительный или просроченный Refresh Token. Пожалуйста, войдите снова.");      
            }                                                                                                               
                                                                                                                            
            var newAccessToken = GenerateAccessToken(user);                                                                 
            var newRefreshToken = GenerateRefreshToken();                                                                   
                                                                                                                            
            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(GetRefreshTokenDays());
            await _context.SaveChangesAsync();

            return AuthResult.Ok(newAccessToken, newRefreshToken, user);
        }
                                                                                                                            
        public async Task ForgotPasswordAsync(ForgotPasswordDto dto)                                                        
        {                                                                                                                   
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);                                 
            if (user == null)                                                                                               
            {                                                                                                               
                // Не раскрываем существование email — просто выходим                                                       
                return;                                                                                                     
            }                                                                                                               
                                                                                                                            
            var tokenBytes = new byte[32];                                                                                  
            using (var rng = RandomNumberGenerator.Create())                                                                
            {                                                
                rng.GetBytes(tokenBytes);                                                                                   
            }                                                                                                               
            string resetToken = Convert.ToHexString(tokenBytes);                                                            
                                                                                                                            
            user.PasswordResetToken = resetToken;                                                                           
            user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);                                                    
            await _context.SaveChangesAsync();                                                                              
                                                                                                                            
            try                                                                                                                                               
            {                                                                                                                                                 
                await _emailService.SendPasswordResetAsync(user, resetToken);                                                                                 
            }                                                                                                                                                 
            catch (Exception ex)                                                                                                                              
            {                                                                                                                                                 
                Console.WriteLine($"Ошибка отправки письма сброса пароля: {ex.Message}");                                                                     
            }              
        }                                                                                                                   
                                                                                                                            
        public async Task<AuthResult> ResetPasswordAsync(ResetPasswordDto dto)                                              
        {                                                                                                                   
            var user = await _context.Users.FirstOrDefaultAsync(u =>                                                        
                u.Email == dto.Email &&                                                                                     
                u.PasswordResetToken == dto.Token);                                                                         
                                                                                                                            
            if (user == null || user.PasswordResetTokenExpiry <= DateTime.UtcNow)                                           
            {                                                                                                               
                return AuthResult.Fail("Недействительный или просроченный код сброса пароля.");                             
            }                                                                                                               
                                                                                                                            
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiry = null;

            // Инвалидируем refresh-токен: после сброса пароля все сессии должны завершиться
            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;

            await _context.SaveChangesAsync();

            return new AuthResult { Success = true };
        }
                                                                                                                            
        private string GenerateAccessToken(User user)                                                                       
        {                                                                                                                   
            var claims = new List<Claim>                                                                                    
            {                                                                                                               
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),                                                   
                new Claim(ClaimTypes.Email, user.Email),                                                                    
                new Claim(ClaimTypes.Role, user.Role)                                                                       
            };                                                                                                              
                                                                                                                            
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private int GetRefreshTokenDays() => _jwtSettings.RefreshTokenDays;
                                                                                                                            
        private string GenerateRefreshToken()                                                                               
        {                                                                                                                   
            var randomNumber = new byte[64];                                                                                
            using var rng = RandomNumberGenerator.Create();                                                                 
            rng.GetBytes(randomNumber);                                                                                     
            return Convert.ToBase64String(randomNumber);                                                                    
        }                                                                                                                   
    }                                                                                                                       
}     
