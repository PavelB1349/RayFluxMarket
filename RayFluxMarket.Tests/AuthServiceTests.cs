using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using RayFluxMarket.Configuration;
using RayFluxMarket.Data;
using RayFluxMarket.Models.DTOs;
using RayFluxMarket.Models.Entities;
using RayFluxMarket.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Xunit;

namespace RayFluxMarket.Tests
{
    public class AuthServiceTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly Mock<IEmailService> _emailServiceMock;
        private readonly AuthService _authService;
        private readonly JwtSettings _jwtSettings;

        public AuthServiceTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);

            _jwtSettings = new JwtSettings
            {
                Key = "super-secret-key-for-tests-only-1234567890",
                Issuer = "TestIssuer",
                Audience = "TestAudience",
                AccessTokenMinutes = 15,
                RefreshTokenDays = 7
            };

            _emailServiceMock = new Mock<IEmailService>();
            _emailServiceMock
                .Setup(e => e.SendWelcomeAsync(It.IsAny<User>()))
                .Returns(Task.CompletedTask);
            _emailServiceMock
                .Setup(e => e.SendPasswordResetAsync(It.IsAny<User>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            _authService = new AuthService(
                _context,
                Options.Create(_jwtSettings),
                _emailServiceMock.Object);
        }

        public void Dispose()
        {
            _context.Dispose();
        }

        // ---------- Register ----------

        [Fact]
        public async Task RegisterAsync_NewUser_ReturnsSuccessWithTokens()
        {
            var dto = new RegisterDto { Email = "new@test.com", Password = "Password123!" };

            var result = await _authService.RegisterAsync(dto);

            Assert.True(result.Success);
            Assert.NotNull(result.AccessToken);
            Assert.NotNull(result.RefreshToken);
            Assert.NotNull(result.User);
            Assert.Equal("new@test.com", result.User!.Email);
            Assert.Equal("User", result.User.Role);
            Assert.NotEqual("Password123!", result.User.PasswordHash);
            Assert.True(BCrypt.Net.BCrypt.Verify("Password123!", result.User.PasswordHash));
        }

        [Fact]
        public async Task RegisterAsync_NewUser_PersistsUserInDatabase()
        {
            var dto = new RegisterDto { Email = "persist@test.com", Password = "Password123!" };

            await _authService.RegisterAsync(dto);

            var saved = await _context.Users.SingleOrDefaultAsync(u => u.Email == "persist@test.com");
            Assert.NotNull(saved);
            Assert.NotNull(saved!.RefreshToken);
            Assert.NotNull(saved.RefreshTokenExpiryTime);
        }

        [Fact]
        public async Task RegisterAsync_NewUser_SendsWelcomeEmail()
        {
            var dto = new RegisterDto { Email = "welcome@test.com", Password = "Password123!" };

            await _authService.RegisterAsync(dto);

            _emailServiceMock.Verify(
                e => e.SendWelcomeAsync(It.Is<User>(u => u.Email == "welcome@test.com")),
                Times.Once);
        }

        [Fact]
        public async Task RegisterAsync_DuplicateEmail_ReturnsFail()
        {
            _context.Users.Add(new User
            {
                Email = "dup@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("x"),
                Role = "User"
            });
            await _context.SaveChangesAsync();

            var result = await _authService.RegisterAsync(
                new RegisterDto { Email = "dup@test.com", Password = "Password123!" });

            Assert.False(result.Success);
            Assert.Contains("уже зарегистрирован", result.ErrorMessage);
        }

        [Fact]
        public async Task RegisterAsync_EmailServiceThrows_StillReturnsSuccess()
        {
            _emailServiceMock
                .Setup(e => e.SendWelcomeAsync(It.IsAny<User>()))
                .ThrowsAsync(new Exception("SMTP down"));

            var result = await _authService.RegisterAsync(
                new RegisterDto { Email = "smtp@test.com", Password = "Password123!" });

            Assert.True(result.Success);
        }

        [Fact]
        public async Task RegisterAsync_AccessTokenContainsCorrectClaims()
        {
            var result = await _authService.RegisterAsync(
                new RegisterDto { Email = "claims@test.com", Password = "Password123!" });

            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(result.AccessToken);

            // AuthService использует ClaimTypes.*, которые сериализуются в длинные URI
            Assert.Contains(token.Claims, c =>
                c.Type == ClaimTypes.Email && c.Value == "claims@test.com");
            Assert.Contains(token.Claims, c =>
                c.Type == ClaimTypes.Role && c.Value == "User");
            Assert.Contains(token.Claims, c =>
                c.Type == ClaimTypes.NameIdentifier && c.Value == result.User!.Id.ToString());
        }

        // ---------- Login ----------

        [Fact]
        public async Task LoginAsync_ValidCredentials_ReturnsSuccess()
        {
            await SeedUserAsync("login@test.com", "Password123!");

            var result = await _authService.LoginAsync(
                new LoginDto { Email = "login@test.com", Password = "Password123!" });

            Assert.True(result.Success);
            Assert.NotNull(result.AccessToken);
            Assert.NotNull(result.RefreshToken);
        }

        [Fact]
        public async Task LoginAsync_ValidCredentials_UpdatesRefreshTokenInDb()
        {
            var user = await SeedUserAsync("refresh@test.com", "Password123!");
            var oldRefresh = user.RefreshToken;

            await _authService.LoginAsync(
                new LoginDto { Email = "refresh@test.com", Password = "Password123!" });

            var updated = await _context.Users.FindAsync(user.Id);
            Assert.NotNull(updated!.RefreshToken);
            Assert.NotEqual(oldRefresh, updated.RefreshToken);
        }

        [Fact]
        public async Task LoginAsync_WrongPassword_ReturnsFail()
        {
            await SeedUserAsync("wrong@test.com", "Password123!");

            var result = await _authService.LoginAsync(
                new LoginDto { Email = "wrong@test.com", Password = "WrongPassword" });

            Assert.False(result.Success);
            Assert.Contains("Неверный", result.ErrorMessage);
        }

        [Fact]
        public async Task LoginAsync_UnknownEmail_ReturnsFail()
        {
            var result = await _authService.LoginAsync(
                new LoginDto { Email = "ghost@test.com", Password = "Password123!" });

            Assert.False(result.Success);
            Assert.Contains("Неверный", result.ErrorMessage);
        }

        // ---------- RefreshToken ----------

        [Fact]
        public async Task RefreshTokenAsync_ValidToken_ReturnsNewTokens()
        {
            var user = await SeedUserAsync("rt@test.com", "Password123!");
            var oldRefresh = user.RefreshToken;

            var result = await _authService.RefreshTokenAsync(
                new RefreshTokenDto { RefreshToken = oldRefresh! });

            Assert.True(result.Success);
            Assert.NotNull(result.AccessToken);
            Assert.NotNull(result.RefreshToken);
            Assert.NotEqual(oldRefresh, result.RefreshToken);
        }

        [Fact]
        public async Task RefreshTokenAsync_EmptyToken_ReturnsFail()
        {
            var result = await _authService.RefreshTokenAsync(
                new RefreshTokenDto { RefreshToken = "" });

            Assert.False(result.Success);
            Assert.Contains("не передан", result.ErrorMessage);
        }

        [Fact]
        public async Task RefreshTokenAsync_UnknownToken_ReturnsFail()
        {
            var result = await _authService.RefreshTokenAsync(
                new RefreshTokenDto { RefreshToken = "nonexistent-token" });

            Assert.False(result.Success);
        }

        [Fact]
        public async Task RefreshTokenAsync_ExpiredToken_ReturnsFail()
        {
            var user = await SeedUserAsync("expired@test.com", "Password123!");
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(-1);
            await _context.SaveChangesAsync();

            var result = await _authService.RefreshTokenAsync(
                new RefreshTokenDto { RefreshToken = user.RefreshToken! });

            Assert.False(result.Success);
            Assert.Contains("просроченный", result.ErrorMessage);
        }

        // ---------- ForgotPassword ----------

        [Fact]
        public async Task ForgotPasswordAsync_ExistingUser_SetsResetTokenAndSendsEmail()
        {
            var user = await SeedUserAsync("forgot@test.com", "Password123!");

            await _authService.ForgotPasswordAsync(new ForgotPasswordDto { Email = "forgot@test.com" });

            var updated = await _context.Users.FindAsync(user.Id);
            Assert.NotNull(updated!.PasswordResetToken);
            Assert.NotNull(updated.PasswordResetTokenExpiry);
            Assert.True(updated.PasswordResetTokenExpiry > DateTime.UtcNow);

            _emailServiceMock.Verify(
                e => e.SendPasswordResetAsync(
                    It.Is<User>(u => u.Id == user.Id),
                    It.Is<string>(t => t == updated.PasswordResetToken)),
                Times.Once);
        }

        [Fact]
        public async Task ForgotPasswordAsync_UnknownEmail_DoesNotThrow()
        {
            // Не должно падать и не должно раскрывать существование email
            await _authService.ForgotPasswordAsync(new ForgotPasswordDto { Email = "ghost@test.com" });

            _emailServiceMock.Verify(
                e => e.SendPasswordResetAsync(It.IsAny<User>(), It.IsAny<string>()),
                Times.Never);
        }

        // ---------- ResetPassword ----------

        [Fact]
        public async Task ResetPasswordAsync_ValidToken_ChangesPasswordAndClearsTokens()
        {
            var user = await SeedUserAsync("reset@test.com", "OldPassword123!");
            user.PasswordResetToken = "valid-token";
            user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);
            await _context.SaveChangesAsync();

            var result = await _authService.ResetPasswordAsync(new ResetPasswordDto
            {
                Email = "reset@test.com",
                Token = "valid-token",
                NewPassword = "NewPassword456!"
            });

            Assert.True(result.Success);

            var updated = await _context.Users.FindAsync(user.Id);
            Assert.True(BCrypt.Net.BCrypt.Verify("NewPassword456!", updated!.PasswordHash));
            Assert.Null(updated.PasswordResetToken);
            Assert.Null(updated.PasswordResetTokenExpiry);
            Assert.Null(updated.RefreshToken);
            Assert.Null(updated.RefreshTokenExpiryTime);
        }

        [Fact]
        public async Task ResetPasswordAsync_InvalidToken_ReturnsFail()
        {
            await SeedUserAsync("badreset@test.com", "OldPassword123!");

            var result = await _authService.ResetPasswordAsync(new ResetPasswordDto
            {
                Email = "badreset@test.com",
                Token = "wrong-token",
                NewPassword = "NewPassword456!"
            });

            Assert.False(result.Success);
            Assert.Contains("Недействительный", result.ErrorMessage);
        }

        [Fact]
        public async Task ResetPasswordAsync_ExpiredToken_ReturnsFail()
        {
            var user = await SeedUserAsync("expreset@test.com", "OldPassword123!");
            user.PasswordResetToken = "expired-token";
            user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(-1);
            await _context.SaveChangesAsync();

            var result = await _authService.ResetPasswordAsync(new ResetPasswordDto
            {
                Email = "expreset@test.com",
                Token = "expired-token",
                NewPassword = "NewPassword456!"
            });

            Assert.False(result.Success);
            Assert.Contains("просроченный", result.ErrorMessage);
        }

        // ---------- Helpers ----------

        private async Task<User> SeedUserAsync(string email, string password)
        {
            var user = new User
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = "User",
                RefreshToken = Guid.NewGuid().ToString("N"),
                RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7)
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }
    }
}
