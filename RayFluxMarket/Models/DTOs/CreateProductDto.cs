using System.ComponentModel.DataAnnotations;

namespace RayFluxMarket.Models.DTOs
{
    public class CreateProductDto
    {
        public string Name { get; set; } = string.Empty;       
        public string? Description { get; set; }     
        public decimal Price { get; set; }

        [Range(0, 10000, ErrorMessage = "Остаток не может быть отрицательным.")]
        public int StockQuantity { get; set; }

        public string? Season { get; set; }
        public string? Collection { get; set; }
        public int CategoryId { get; set; }
        public int BrandId { get; set; }

        public List<string> ImageUrls { get; set; } = new List<string>();

        public List<int> MaterialIds { get; set; } = new List<int>();
    }
}
