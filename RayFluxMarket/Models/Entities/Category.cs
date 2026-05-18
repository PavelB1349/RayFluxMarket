using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace RayFluxMarket.Models.Entities
{
    public class Category
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        // Для иерархии (Мужское -> Одежда)
        public int? ParentCategoryId { get; set; }
        [JsonIgnore]
        public Category? ParentCategory { get; set; }

        public List<Category> SubCategories { get; set; } = new();
    }
}