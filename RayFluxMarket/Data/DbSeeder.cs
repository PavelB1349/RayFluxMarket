using Microsoft.EntityFrameworkCore;
using RayFluxMarket.Models.Entities;

namespace RayFluxMarket.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(AppDbContext context)
        {
            // 1. Пользователи
            if (!await context.Users.AnyAsync(u => u.Email == "admin@mail.com"))
            {
                context.Users.Add(new User { Email = "admin@mail.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"), Role = "Admin" });
            }
            if (!await context.Users.AnyAsync(u => u.Email == "user@mail.com"))
            {
                context.Users.Add(new User { Email = "user@mail.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("User123!"), Role = "User" });
            }
            await context.SaveChangesAsync();

            // 2. Бренды
            if (!await context.Brands.AnyAsync())
            {
                context.Brands.AddRange(
                    new Brand { Name = "Nike", Description = "Спортивная классика", LogoUrl = "" },
                    new Brand { Name = "Adidas", Description = "Стиль и комфорт", LogoUrl = "" },
                    new Brand { Name = "RayFlux Premium", Description = "Наш бренд", LogoUrl = "" }
                );
                await context.SaveChangesAsync();
            }

            // 3. Материалы
            if (!await context.Materials.AnyAsync())
            {
                context.Materials.AddRange(
                    new Material { Name = "Органический хлопок" },
                    new Material { Name = "Полиэстер" },
                    new Material { Name = "Шерсть" },
                    new Material { Name = "Эластан" }
                );
                await context.SaveChangesAsync();
            }

            // 4. Категории
            if (!await context.Categories.AnyAsync())
            {
                var catMens = new Category { Name = "Мужская одежда" };
                var catWomens = new Category { Name = "Женская одежда" };
                context.Categories.AddRange(catMens, catWomens);
                await context.SaveChangesAsync(); // Сохраняем, чтобы получить ID

                context.Categories.AddRange(
                    new Category { Name = "Худи", ParentCategoryId = catMens.Id },
                    new Category { Name = "Платья", ParentCategoryId = catWomens.Id },
                    new Category { Name = "Верхняя одежда", ParentCategoryId = catMens.Id }
                );
                await context.SaveChangesAsync();
            }

            // 5. Товары
            if (await context.Products.CountAsync() < 5)
            {
                var nike = await context.Brands.FirstOrDefaultAsync(b => b.Name == "Nike");
                var rayflux = await context.Brands.FirstOrDefaultAsync(b => b.Name == "RayFlux Premium");
                var catHoodies = await context.Categories.FirstOrDefaultAsync(c => c.Name == "Худи");
                var catJackets = await context.Categories.FirstOrDefaultAsync(c => c.Name == "Верхняя одежда");

                if (nike != null && catHoodies != null)
                {
                    context.Products.Add(new Product
                    {
                        Name = "Nike Club Fleece",
                        Description = "Удобное мужское худи.",
                        Price = 35000,
                        BrandId = nike.Id,
                        CategoryId = catHoodies.Id,
                        StockQuantity = 50
                    });
                }

                if (rayflux != null && catJackets != null)
                {
                    context.Products.Add(new Product
                    {
                        Name = "RayFlux Winter Coat",
                        Description = "Зимнее пальто.",
                        Price = 120000,
                        BrandId = rayflux.Id,
                        CategoryId = catJackets.Id,
                        StockQuantity = 100
                    });
                }
                await context.SaveChangesAsync();
            }
        }
    }
}