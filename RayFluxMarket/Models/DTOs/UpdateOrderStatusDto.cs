using System.ComponentModel.DataAnnotations;

namespace RayFluxMarket.Models.DTOs
{
    public class UpdateOrderStatusDto
    {
        public string Status { get; set; } = string.Empty;
    }
}
