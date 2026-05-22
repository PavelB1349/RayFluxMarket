using System.ComponentModel.DataAnnotations;

namespace RayFluxMarket.Models.DTOs
{
    public class AddToCartDto
        //то, что клиент присылает нам, когда жмет кнопку «Добавить в корзину»:
    {
        [Required]
        public int ProductId { get; set; }

        [Range(1, 100, ErrorMessage = "Количество должно быть от 1 до 100.")]
        public int Quantity { get; set; } = 1;
    }
}
