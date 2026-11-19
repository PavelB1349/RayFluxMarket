namespace RayFluxMarket.Services
{
    public interface IPaymentService
    {
        // Создаёт Stripe Checkout-сессию для заказа и возвращает URL страницы оплаты
        Task<string> CreateCheckoutSessionAsync(int userId, int orderId, string domain);

        // Обрабатывает webhook от Stripe (проверка подписи, обновление статуса, отправка чека)
        Task HandleWebhookAsync(string json, string stripeSignature);
    }
}
