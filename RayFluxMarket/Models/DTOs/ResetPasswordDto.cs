using System.ComponentModel.DataAnnotations;

namespace RayFluxMarket.Models.DTOs
{
    public class ResetPasswordDto
    {
        [Required]
        [EmailAddress(ErrorMessage = "Некорректный формат Email.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Токен сброса обязателен.")]
        public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "Новый пароль обязателен.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Пароль должен быть не менее 6 символов.")]
        public string NewPassword { get; set; } = string.Empty;
    }
}
