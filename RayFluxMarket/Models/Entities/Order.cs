using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RayFluxMarket.Models.Entities
{
    public class Order
    {
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; } // Наша заглушка, пока нет авторизации

        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        // Статус заказа (пока строкой: New, Paid, Shipped, Cancelled)
        [Required]
        public string Status { get; set; } = "New";

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        // Навигационное свойство: список позиций в этом заказе
        public List<OrderItem> OrderItems { get; set; } = new();
    }
}
