using Stripe;
using Stripe.Checkout;
using RayFluxMarket.Models.Entities;

namespace RayFluxMarket.Services
{
    public class PaymentService : IPaymentService
    {
        public async Task<string> CreateCheckoutSessionAsync(Order order, string domain)
        {
            var lineItems = new List<SessionLineItemOptions>();

            foreach (var item in order.OrderItems)
            {
                lineItems.Add(new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        UnitAmount = (long)(item.Price * 100), // Цена в минимальных единицах (например, $10.00 -> 1000)
                        Currency = "kzt", // Можно указать eur или другую валюту
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = item.Product?.Name ?? $"Товар #{item.ProductId}",
                            Description = $"Количество: {item.Quantity} шт."
                        },
                    },
                    Quantity = item.Quantity,
                });
            }

            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = lineItems,
                Mode = "payment",
                // Куда перенаправить покупателя после успешной оплаты
                SuccessUrl = $"{domain}/checkout-success?orderId={order.Id}",
                // Куда перенаправить, если покупатель отменил оплату
                CancelUrl = $"{domain}/checkout-cancel?orderId={order.Id}",
                // Сохраняем ID нашего заказа в метаданных Stripe, чтобы потом связать с ним Webhook
                Metadata = new Dictionary<string, string>
                {
                    { "OrderId", order.Id.ToString() }
                }
            };

            var service = new SessionService();
            Session session = await service.CreateAsync(options);

            return session.Url; // Возвращаем прямую ссылку на страницу оплаты Stripe
        }
    }
}