using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace RayFluxMarket.Models.Entities
{
    public class CartItem
    {
        public int Id { get; set; }

        // К какому пользователю относится эта позиция в корзине
        [Required]
        public int UserId { get; set; }

        // Связь с товаром
        [Required]
        public int ProductId { get; set; }

        [JsonIgnore] // Чтобы не было зацикливания при сериализации в JSON
        public Product? Product { get; set; }

        // Количество товара в корзине
        [Range(1, 100, ErrorMessage = "Количество товара должно быть от 1 до 100.")]
        public int Quantity { get; set; }

        // Дата добавления (полезно, чтобы потом чистить старые забытые корзины)
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
