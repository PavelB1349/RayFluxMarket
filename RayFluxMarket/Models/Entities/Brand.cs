using System.ComponentModel.DataAnnotations;

namespace RayFluxMarket.Models.Entities
{
    public class Brand
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }
        public string? LogoUrl { get; set; }
    }
}