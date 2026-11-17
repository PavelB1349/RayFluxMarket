using Microsoft.EntityFrameworkCore;                                                                                        
using Microsoft.IdentityModel.Tokens;                                                                                       
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
        private readonly IConfiguration _configuration;                                                                     
        private readonly IEmailService _emailService;                                                                       
                                                                                                                            
        public AuthService(AppDbContext context, IConfiguration configuration, IEmailService emailService)                  
        {                                                                                                                   
            _context = context;                                                                                             
            _configuration = configuration;                                                                                 
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
                                                                                                                            
            var accessToken = GenerateAccessToken(newUser);                                                                 
            var refreshToken = GenerateRefreshToken();                                                                      
                                                                                                                            
            newUser.RefreshToken = refreshToken;                                                                            
            newUser.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);                                                    
                                                                                                                            
            _context.Users.Add(newUser);                                                                                    
            await _context.SaveChangesAsync();                                                                              
                                                                                                                            
            try                                                                                                             
            {                                                                                                               
                string subject = "Добро пожаловать в RayFluxMarket!";                                                       
                string htmlMessage = $@"                                                                                    
                    <div style='font-family: Arial, sans-serif; padding: 20px; color: #333;'>                               
                        <h2 style='color: #4f46e5;'>Приветствуем в RayFluxMarket!</h2>                                      
                        <p>Спасибо за регистрацию в нашем интернет-магазине.</p>                                            
                        <p>Ваш аккаунт (<b>{newUser.Email}</b>) успешно создан, и вы можете приступать к покупкам.</p>      
                        <br>                                                                                                
                        <p>С уважением, <br><b>Команда RayFluxMarket</b></p>                                                
                    </div>";                                                                                                
                                                                                                                            
                await _emailService.SendEmailAsync(newUser.Email, subject, htmlMessage);                                    
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
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);                                                       
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
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);                                                       
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
                string subject = "Сброс пароля — RayFluxMarket";                                                            
                string htmlMessage = $@"                                                                                    
                    <div style='font-family: Arial, sans-serif; padding: 20px; color: #333;'>                               
                        <h2 style='color: #4f46e5;'>Сброс пароля</h2>                                                       
                        <p>Вы запросили сброс пароля для вашего аккаунта <b>{user.Email}</b>.</p>                           
                        <p>Ваш одноразовый код для сброса пароля:</p>                                                       
                        <div style='background-color: #f3f4f6; padding: 12px; font-size: 18px; font-weight: bold;           
font-family: monospace; letter-spacing: 2px; text-align: center; margin: 15px 0;'>                                          
                            {resetToken}                                                                                    
                        </div>                                                                                              
                        <p>Код действителен в течение 1 часа.</p>                                                           
                        <p>Если вы не запрашивали сброс пароля, просто проигнорируйте это письмо.</p>                       
                        <br>                                                                                                
                        <p>С уважением, <br><b>Команда RayFluxMarket</b></p>                                                
                    </div>";                                                                                                
                                                                                                                            
                await _emailService.SendEmailAsync(user.Email, subject, htmlMessage);                                       
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
                                                                                                                            
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));                         
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);                                         
                                                                                                                            
            var token = new JwtSecurityToken(                                                                               
                issuer: _configuration["Jwt:Issuer"],                                                                       
                audience: _configuration["Jwt:Audience"],                                                                   
                claims: claims,                                                                                             
                expires: DateTime.UtcNow.AddMinutes(15),                                                                    
                signingCredentials: creds                                                                                   
            );                                                                                                              
                                                                                                                            
            return new JwtSecurityTokenHandler().WriteToken(token);                                                         
        }                                                                                                                   
                                                                                                                            
        private string GenerateRefreshToken()                                                                               
        {                                                                                                                   
            var randomNumber = new byte[64];                                                                                
            using var rng = RandomNumberGenerator.Create();                                                                 
            rng.GetBytes(randomNumber);                                                                                     
            return Convert.ToBase64String(randomNumber);                                                                    
        }                                                                                                                   
    }                                                                                                                       
}     