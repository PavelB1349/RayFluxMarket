using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RayFluxMarket.Models.Entities
{
    public class Product
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        // Сезонность и коллекция (просто строки для начала)
        public string? Season { get; set; } // Зима, Лето, Демисезон
        public string? Collection { get; set; } // New Arrival, Sale

        // Связь с Брендом
        public int BrandId { get; set; }
        public Brand? Brand { get; set; }

        // Связь с Категорией
        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        // Список изображений (один товар — много фото)
        public List<ProductImage> Images { get; set; } = new();

        // Список материалов (хлопок, шерсть...)
        public List<Material> Materials { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}