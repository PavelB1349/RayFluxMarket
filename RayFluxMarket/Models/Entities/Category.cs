using System.ComponentModel.DataAnnotations;

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
        public Category? ParentCategory { get; set; }

        public List<Category> SubCategories { get; set; } = new();
    }
}