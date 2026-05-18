namespace RayFluxMarket.Models.Entities
{
    public class ProductImage
    {
        public int Id { get; set; }
        public string Url { get; set; } = string.Empty;
        public bool IsPrimary { get; set; } // Главное фото или нет
        public int ProductId { get; set; }
    }
}