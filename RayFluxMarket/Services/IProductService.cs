using RayFluxMarket.Models.DTOs;

namespace RayFluxMarket.Services
{
    public interface IProductService
    {
        Task<IEnumerable<ProductDto>> GetProductsAsync(ProductQueryParameters query);
        Task<ProductDto> GetProductByIdAsync(int id);
        Task<ProductDto> CreateProductAsync(CreateProductDto dto);
        Task UpdateProductAsync(int id, CreateProductDto dto);
        Task DeleteProductAsync(int id);
        Task<string> UploadImageAsync(int productId, IFormFile file);
    }
}
