using System.ComponentModel.DataAnnotations;

namespace RayFluxMarket.Models.DTOs
{
    public class ForgotPasswordDto
    {
        [Required(ErrorMessage = "Email обязателен.")]
        [EmailAddress(ErrorMessage = "Некорректный формат Email.")]
        public string Email { get; set; } = string.Empty;
    }
}
