namespace RayFluxMarket.Models.Entities
{
    public class Material
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<Product> Products { get; set; } = new(); // Многие-ко-многим
    }
}