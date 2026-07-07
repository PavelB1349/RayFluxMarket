using BCrypt.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RayFluxMarket.Data;
using RayFluxMarket.Models.DTOs;
using RayFluxMarket.Models.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace RayFluxMarket.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
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

            // Проверяем, подходит ли введенный чистый пароль к хэшу из базы
            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
            if (!isPasswordValid)
            {
                return BadRequest(new { message = "Неверный пароль." });
            }

            //// Если всё верно — временно возвращаем сообщение и данные пользователя
            //return Ok(new
            //{
            //    message = "Вы успешно вошли!",
            //    userId = user.Id,
            //    email = user.Email,
            //    role = user.Role
            //});




            // --- ГЕНЕРАЦИЯ JWT-ТОКЕНА ---

            // 1. Создаем "Claims" (Утверждения) — данные, которые зашьем внутрь токена
            var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), // Зашиваем ID
            new Claim(ClaimTypes.Email, user.Email),                  // Зашиваем Email
            new Claim(ClaimTypes.Role, user.Role)                     // Зашиваем Роль (User/Admin)
        };

            // 2. Берем наш секретный ключ из appsettings.json
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // 3. Собираем токен воедино
            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7), // Токен будет жить 7 дней
                signingCredentials: creds
            );

            // 4. Превращаем токен в красивую длинную строку
            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            // Возвращаем токен клиенту!
            return Ok(new
            {
                message = "Вы успешно вошли!",
                token = tokenString
            });
        }
    }
}
