namespace RayFluxMarket.Services
{
    public interface IFileService
    {
        Task<string> UploadProductImageAsync(IFormFile file);
        void DeleteProductImage(string relativePath);// Удаляет изображение по относительному пути
    }
}
