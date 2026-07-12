namespace RayFluxMarket.Services
{
    public interface IFileService
    {
        Task<string> UploadProductImageAsync(IFormFile file);
    }
}
