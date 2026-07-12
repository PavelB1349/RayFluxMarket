namespace RayFluxMarket.Models.DTOs
{
    public class ProductQueryParameters
    {
        // Пагинация (по умолчанию 1-я страница, по 10 товаров)
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        // Поиск по строке (в названии или описании)
        public string? Search { get; set; }

        // Фильтры
        public int? CategoryId { get; set; }
        public int? BrandId { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }

        // Сортировка (например: "price_asc", "price_desc", "name")
        public string? SortBy { get; set; }
    }
}
