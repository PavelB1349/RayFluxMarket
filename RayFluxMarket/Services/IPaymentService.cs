using RayFluxMarket.Models.Entities;

namespace RayFluxMarket.Services
{
    public interface IPaymentService
    {
        Task<string> CreateCheckoutSessionAsync(Order order, string domain);
    }
}
