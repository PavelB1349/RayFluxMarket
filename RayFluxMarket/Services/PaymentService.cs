using Microsoft.EntityFrameworkCore;
using RayFluxMarket.Data;
using RayFluxMarket.Models.Entities;
using RayFluxMarket.Models.Enums;
using Stripe;
using Stripe.Checkout;
using System.Text;

namespace RayFluxMarket.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;
        private readonly string _webhookSecret;

        public PaymentService(AppDbContext context, IEmailService emailService, IConfiguration configuration)
        {
            _context = context;
            _emailService = emailService;
            _webhookSecret = configuration["Stripe:WebhookSecret"]!;
        }

        public async Task<string> CreateCheckoutSessionAsync(int orderId, string domain)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                throw new KeyNotFoundException($"Заказ с ID {orderId} не найден.");
            }

            if (order.Status == OrderStatus.Paid)
            {
                throw new InvalidOperationException("Этот заказ уже оплачен.");
            }

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

        public async Task HandleWebhookAsync(string json, string stripeSignature)
        {
            var stripeEvent = EventUtility.ConstructEvent(json, stripeSignature, _webhookSecret);

            if (stripeEvent.Type != "checkout.session.completed")
            {
                return;
            }

            if (stripeEvent.Data.Object is not Session session)
            {
                return;
            }

            if (!session.Metadata.TryGetValue("OrderId", out string? orderIdStr))
            {
                return;
            }

            if (!int.TryParse(orderIdStr, out int orderId))
            {
                return;
            }

            // Подгружаем заказ вместе с пользователем и позициями товара
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null || order.Status == OrderStatus.Paid)
            {
                return;
            }

            order.Status = OrderStatus.Paid;
            await _context.SaveChangesAsync();

            Console.WriteLine($"✅ Заказ #{orderId} успешно оплачен!");

            // Находим пользователя в базе по UserId из заказа
            var user = await _context.Users.FindAsync(order.UserId);

            // Отправляем email-чек пользователю
            if (user != null && !string.IsNullOrEmpty(user.Email))
            {
                await SendPaymentReceiptEmailAsync(order, user);
            }
        }

        // Приватный метод для генерации HTML-чека и отправки письма
        private async Task SendPaymentReceiptEmailAsync(Order order, User user)
        {
            try
            {
                var itemsHtml = new StringBuilder();
                decimal totalAmount = 0;

                foreach (var item in order.OrderItems)
                {
                    decimal itemTotal = item.Price * item.Quantity;
                    totalAmount += itemTotal;
                    string productName = item.Product?.Name ?? $"Товар #{item.ProductId}";

                    itemsHtml.Append($@"
                <tr>
                    <td style='padding: 8px; border-bottom: 1px solid #ddd;'>{productName}</td>
                    <td style='padding: 8px; border-bottom: 1px solid #ddd; text-align: center;'>{item.Quantity} шт.</td>
                    <td style='padding: 8px; border-bottom: 1px solid #ddd; text-align: right;'>{itemTotal:N2} ₸</td>
                </tr>");
                }

                string subject = $"Чек об оплате заказа #{order.Id} — RayFluxMarket";
                string htmlBody = $@"
            <div style='font-family: Arial, sans-serif; padding: 20px; color: #333; max-width: 600px;'>
                <h2 style='color: #16a34a;'>Оплата прошла успешно! 🎉</h2>
                <p>Здравствуйте, <b>{user.Email}</b>!</p>
                <p>Благодарим за покупку в магазине <b>RayFluxMarket</b>. Ваш заказ <b>#{order.Id}</b> передан в обработку.</p>
                
                <h3 style='margin-top: 20px;'>Детали заказа:</h3>
                <table style='width: 100%; border-collapse: collapse; margin-top: 10px;'>
                    <thead>
                        <tr style='background-color: #f3f4f6;'>
                            <th style='padding: 8px; text-align: left;'>Товар</th>
                            <th style='padding: 8px; text-align: center;'>Кол-во</th>
                            <th style='padding: 8px; text-align: right;'>Сумма</th>
                        </tr>
                    </thead>
                    <tbody>
                        {itemsHtml}
                    </tbody>
                </table>

                <div style='margin-top: 15px; text-align: right; font-size: 18px; font-weight: bold;'>
                    Итого оплачено: <span style='color: #16a34a;'>{totalAmount:N2} ₸</span>
                </div>

                <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'>
                <p style='font-size: 12px; color: #777;'>Это автоматический чек. Если у вас возникли вопросы, свяжитесь со службой поддержки.</p>
            </div>";

                await _emailService.SendEmailAsync(user.Email, subject, htmlBody);
                Console.WriteLine($"📧 Чек об оплате заказа #{order.Id} успешно отправлен на {user.Email}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Ошибка при отправке чека об оплате: {ex.Message}");
            }
        }
    }
}
