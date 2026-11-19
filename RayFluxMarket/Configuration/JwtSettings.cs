using System.ComponentModel.DataAnnotations;

namespace RayFluxMarket.Configuration
{
    public class JwtSettings
    {
        public const string SectionName = "Jwt";

        [Required(ErrorMessage = "Jwt:Key обязателен.")]
        [MinLength(32, ErrorMessage = "Jwt:Key должен быть не короче 32 символов.")]
        public string Key { get; set; } = string.Empty;

        [Required]
        public string Issuer { get; set; } = string.Empty;

        [Required]
        public string Audience { get; set; } = string.Empty;

        [Range(1, 1440)]
        public int AccessTokenMinutes { get; set; } = 15;

        [Range(1, 365)]
        public int RefreshTokenDays { get; set; } = 7;
    }
}
