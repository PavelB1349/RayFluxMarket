using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace RayFluxMarket.Models.Entities
{
    public class OrderItem
    {
        public int Id { get; set; }

        // Связь с шапкой заказа
        public int OrderId { get; set; }
        [JsonIgnore]
        public Order? Order { get; set; }

        // Связь с товаром
        public int ProductId { get; set; }
        public Product? Product { get; set; }

        // Количество на момент покупки
        [Range(1, 100)]
        public int Quantity { get; set; }

        // Цена на момент покупки (фиксируем историю!)
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }
    }
}
