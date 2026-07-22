using System.ComponentModel.DataAnnotations;

namespace RayFluxMarket.Models.DTOs
{
    public class RefreshTokenDto
    {
        [Required(ErrorMessage = "Refresh Token обязателен.")]
        public string RefreshToken { get; set; } = string.Empty;
    }
}
