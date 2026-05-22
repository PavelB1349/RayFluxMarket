namespace RayFluxMarket.Models.DTOs
{
    public class CartDto
    {
        public List<CartItemSummaryDto> Items { get; set; } = new List<CartItemSummaryDto>();
        public decimal TotalPrice => Items.Sum(item => item.Price * item.Quantity);
    }
    public class CartItemSummaryDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? MainImageUrl { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public decimal TotalItemPrice => Price * Quantity; // Цена за одну позицию (например, 2 худи по 5000 = 10000)

    }
}
