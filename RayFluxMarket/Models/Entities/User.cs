using System.ComponentModel.DataAnnotations;

namespace RayFluxMarket.Models.Entities
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        // Хэш пароля (зашифрованная строка)
        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        // Роль пользователя: по умолчанию обычный покупатель ("User"), но сможет стать и "Admin"
        [Required]
        public string Role { get; set; } = "User";
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiryTime { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? PasswordResetToken { get; set; }
        public DateTime? PasswordResetTokenExpiry { get; set; }
    }
}
