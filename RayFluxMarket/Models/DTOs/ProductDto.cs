namespace RayFluxMarket.Models.DTOs
{
    public class ProductDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public string? Season { get; set; }
        public string? Collection { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        public int CategoryId { get; set; }
        public string? CategoryName { get; set; }

        public int BrandId { get; set; }
        public string? BrandName { get; set; }

        public List<string> ImageUrls { get; set; } = new();
        public string? PrimaryImageUrl { get; set; }

        public List<string> MaterialNames { get; set; } = new();
    }
}
