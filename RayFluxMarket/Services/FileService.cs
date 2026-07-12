namespace RayFluxMarket.Services
{
    public class FileService : IFileService
    {
        private readonly IWebHostEnvironment _environment;

        // IWebHostEnvironment нужен, чтобы сервер знал, где физически на диске находится папка wwwroot
        public FileService(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public async Task<string> UploadProductImageAsync(IFormFile file)
        {
            // 1. Проверяем, что файл вообще прислали
            if (file == null || file.Length == 0)
                throw new ArgumentException("Файл не выбран или пуст.");

            // 2. Защита от хакеров: проверяем расширение файла (чтобы не залили .exe скрипт)
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLower();
            if (!allowedExtensions.Contains(extension))
                throw new ArgumentException("Недопустимый формат файла. Разрешены только JPG, PNG, WEBP.");

            // 3. Генерируем уникальное имя (чтобы фото "1.jpg" не перезаписало чужое "1.jpg")
            var uniqueFileName = $"{Guid.NewGuid()}{extension}";// Guid.NewGuid() генерирует уникальный идентификатор, который мы используем в качестве имени файла, чтобы избежать конфликтов имен.

            // 4. Строим путь: wwwroot/images/products
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "images", "products");

            // Если таких папок еще нет — создаем их на лету
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            // 5. Полный физический путь на жестком диске
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            // 6. Сохраняем файл
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            // 7. Возвращаем относительный путь, который мы запишем в базу данных
            return $"/images/products/{uniqueFileName}";
        }
    }
}
