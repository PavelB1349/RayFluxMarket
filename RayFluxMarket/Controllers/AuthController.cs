using BCrypt.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RayFluxMarket.Data;
using RayFluxMarket.Models.DTOs;
using RayFluxMarket.Models.Entities;
using RayFluxMarket.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace RayFluxMarket.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;

        public AuthController(AppDbContext context, IConfiguration configuration, IEmailService emailService)
        {
            _context = context;
            _configuration = configuration;
            _emailService = emailService;
        }

        // 1. POST: api/Auth/Register (Регистрация нового пользователя)
        [HttpPost("Register")]
        public async Task<IActionResult> Register(RegisterDto dto)
        {
            // Проверяем, не занят ли email
            var userExists = await _context.Users.AnyAsync(u => u.Email == dto.Email);
            if (userExists)
            {
                return BadRequest(new { message = "Пользователь с таким Email уже зарегистрирован." });
            }

            // Хэшируем пароль — превращаем "123456" в нечитаемую строку "$2a$11$..."
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            // Создаем модель для базы данных
            var newUser = new User
            {
                Email = dto.Email,
                PasswordHash = passwordHash,
                Role = "User" // По умолчанию все обычные покупатели
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // --- ОТПРАВКА ПРИВЕТСТВЕННОГО ПИСЬМА ---
            try
            {
                string subject = "Добро пожаловать в RayFluxMarket!";
                string htmlMessage = $@"
                    <div style='font-family: Arial, sans-serif; padding: 20px; color: #333;'>
                        <h2 style='color: #4f46e5;'>Приветствуем в RayFluxMarket! 🚀</h2>
                        <p>Спасибо за регистрацию в нашем интернет-магазине.</p>
                        <p>Ваш аккаунт (<b>{newUser.Email}</b>) успешно создан, и вы можете приступать к покупкам.</p>
                        <br>
                        <p>С уважением, <br><b>Команда RayFluxMarket</b></p>
                    </div>";

                await _emailService.SendEmailAsync(newUser.Email, subject, htmlMessage);
            }
            catch (Exception)
            {
                // Даже если почта по какой-то причине не ушла (например, нет интернета),
                // мы не должны ломать регистрацию пользователя, поэтому просто логируем или игнорируем
            }
            // ----------------------------------------

            return Ok(new { message = "Регистрация прошла успешно!" });
        }

        // 2. POST: api/Auth/Login (Вход в систему)
        [HttpPost("Login")]
        public async Task<IActionResult> Login(LoginDto dto)
        {
            // Ищем пользователя в базе по Email
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null)
            {
                return BadRequest(new { message = "Неверный Email или пароль." });
            }

            // Генерируем Access и Refresh токены
            var accessToken = GenerateAccessToken(user);
            var refreshToken = GenerateRefreshToken();
            // Сохраняем Refresh токен в базе данных
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Успешный вход!",
                accessToken = accessToken,
                refreshToken = refreshToken
            });
        }

        // 3. POST: api/Auth/refresh (Обновление пары токенов)
        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto dto)// здесь мы ожидаем, что клиент отправит JSON с полем "refreshToken"
        {
            if (string.IsNullOrEmpty(dto.RefreshToken))
            {
                return BadRequest(new { message = "Токен обновления не передан." });
            }

            // Ищем пользователя по Refresh-токену в базе
            var user = await _context.Users.FirstOrDefaultAsync(u => u.RefreshToken == dto.RefreshToken);

            if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
            {
                return Unauthorized(new { message = "Недействительный или просроченный Refresh Token. Пожалуйста, войдите снова." });
            }

            // Генерируем новую пару токенов
            var newAccessToken = GenerateAccessToken(user);
            var newRefreshToken = GenerateRefreshToken();

            // Обновляем Refresh-токен в базе (ротация токенов)
            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                accessToken = newAccessToken,
                refreshToken = newRefreshToken
            });
        }
        // --- ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ---

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
                expires: DateTime.UtcNow.AddMinutes(15), // Access Token живет всего 15 минут!
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string GenerateRefreshToken()
        {
            // Создаем криптостойкую случайную строку для Refresh-токена
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }
    }
}
