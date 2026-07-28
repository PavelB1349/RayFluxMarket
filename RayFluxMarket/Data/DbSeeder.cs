using Microsoft.EntityFrameworkCore;
using RayFluxMarket.Models.Entities;

namespace RayFluxMarket.Data
{
    public static class DbSeeder
    {
        public static void Seed(AppDbContext context)
        {
            //context.Database.Migrate();
            context.Database.EnsureCreated();// это гарантирует, что база данных будет создана, если она еще не существует. Это полезно для начальной настройки базы данных при первом запуске приложения.

            // 1. Пользователи
            if (!context.Users.Any(u => u.Email == "admin@mail.com"))// 
            {
                context.Users.Add(new User { Email = "admin@mail.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"), Role = "Admin" });
            }
            if (!context.Users.Any(u => u.Email == "user@mail.com"))
            {
                context.Users.Add(new User { Email = "user@mail.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("User123!"), Role = "User" });
            }
            context.SaveChanges();

            // 2. Бренды (добавляем только если их нет)
            if (!context.Brands.Any())
            {
                context.Brands.AddRange(
                    new Brand { Name = "Nike", Description = "Спортивная классика", LogoUrl = "" },
                    new Brand { Name = "Adidas", Description = "Стиль и комфорт", LogoUrl = "" },
                    new Brand { Name = "RayFlux Premium", Description = "Наш бренд", LogoUrl = "" }
                );
                context.SaveChanges();
            }

            // 3. Материалы (добавляем только если их нет)
            if (!context.Materials.Any())
            {
                context.Materials.AddRange(
                    new Material { Name = "Органический хлопок" },
                    new Material { Name = "Полиэстер" },
                    new Material { Name = "Шерсть" },
                    new Material { Name = "Эластан" }
                );
                context.SaveChanges();
            }

            // 4. Категории (добавляем только если их нет)
            if (!context.Categories.Any())
            {
                var catMens = new Category { Name = "Мужская одежда" };
                var catWomens = new Category { Name = "Женская одежда" };
                context.Categories.AddRange(catMens, catWomens);
                context.SaveChanges(); // Сохраняем, чтобы получить ID

                context.Categories.AddRange(
                    new Category { Name = "Худи", ParentCategoryId = catMens.Id },
                    new Category { Name = "Платья", ParentCategoryId = catWomens.Id },
                    new Category { Name = "Верхняя одежда", ParentCategoryId = catMens.Id }
                );
                context.SaveChanges();
            }

            // 5. Товары (добавляем только если товаров совсем мало - например, меньше 5)
            // Мы берем ID из базы, чтобы привязать к существующим категориям/брендам
            if (context.Products.Count() < 5)
            {
                var nike = context.Brands.FirstOrDefault(b => b.Name == "Nike");
                var rayflux = context.Brands.FirstOrDefault(b => b.Name == "RayFlux Premium");
                var catHoodies = context.Categories.FirstOrDefault(c => c.Name == "Худи");
                var catJackets = context.Categories.FirstOrDefault(c => c.Name == "Верхняя одежда");

                if (nike != null && catHoodies != null)
                {
                    context.Products.Add(new Product
                    {
                        Name = "Nike Club Fleece",
                        Description = "Удобное мужское худи.",
                        Price = 35000,
                        BrandId = nike.Id,
                        CategoryId = catHoodies.Id
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
                        CategoryId = catJackets.Id
                    });
                }
                context.SaveChanges();
            }
        }
    }
}