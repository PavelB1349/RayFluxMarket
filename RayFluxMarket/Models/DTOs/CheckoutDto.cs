using System.ComponentModel.DataAnnotations;

namespace RayFluxMarket.Models.DTOs;

public class CheckoutDto
{
    [Required(ErrorMessage = "Имя обязательно")]
    public string CustomerName { get; set; } = string.Empty;
    [Required(ErrorMessage = "Телефон обязателен")]
    public string PhoneNumber { get; set; } = string.Empty;
    [Required(ErrorMessage = "Адрес доставки обязателен")]
    public string ShippingAddress { get; set; } = string.Empty;
    public string? Comment { get; set; }
}