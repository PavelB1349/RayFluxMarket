using System.ComponentModel.DataAnnotations;

namespace RayFluxMarket.Models.DTOs
{
    public class UpdateOrderStatusDto
    {
        [Required(ErrorMessage = "Статус обязателен для заполнения.")]
        [StringLength(50, ErrorMessage = "Статус не должен превышать 50 символов.")]
        public string Status { get; set; } = string.Empty;
    }
}
