using Microsoft.EntityFrameworkCore;
using RayFluxMarket.Models.Entities;

namespace RayFluxMarket.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
      
        public DbSet<Category> Categories { get; set; }
        public DbSet<Brand> Brands { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }
        public DbSet<Material> Materials { get; set; }
        public DbSet<CartItem> CartItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Конфигурация связи многие-ко-многим между Product и Material
            modelBuilder.Entity<Product>()
                .HasMany(p => p.Materials)
                .WithMany(m => m.Products);
            //.UsingEntity(j => j.ToTable("ProductMaterials")); // Название таблицы для связи

        }
    }
}