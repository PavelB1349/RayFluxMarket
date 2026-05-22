using System.ComponentModel.DataAnnotations;

namespace RayFluxMarket.Models.DTOs
{
    public class CreateProductDto
    {
        [Required(ErrorMessage = "Название товара обязательно для заполнения.")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Название должно быть от 3 до 100 символов.")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Описание не должно превышать 500 символов.")]
        public string? Description { get; set; }

        [Range(0.01, 10000000, ErrorMessage = "Цена должна быть больше нуля и не превышать 10 000 000.")]
        public decimal Price { get; set; }

        [StringLength(50, ErrorMessage = "Название сезона слишком длинное (макс. 50 символов).")]
        public string? Season { get; set; }

        [StringLength(100, ErrorMessage = "Название коллекции слишком длинное (макс. 100 символов).")]
        public string? Collection { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Укажите корректный ID категории.")]
        public int CategoryId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Укажите корректный ID бренда.")]
        public int BrandId { get; set; }

        public List<string> ImageUrls { get; set; } = new List<string>();

        public List<int> MaterialIds { get; set; } = new List<int>();
    }
}
