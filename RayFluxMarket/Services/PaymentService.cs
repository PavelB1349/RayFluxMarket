using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RayFluxMarket.Configuration;
using RayFluxMarket.Data;
using RayFluxMarket.Models.Entities;
using RayFluxMarket.Models.Enums;
using Stripe;
using Stripe.Checkout;

namespace RayFluxMarket.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;
        private readonly StripeSettings _stripeSettings;

        public PaymentService(AppDbContext context, IEmailService emailService, IOptions<StripeSettings> stripeOptions)
        {
            _context = context;
            _emailService = emailService;
            _stripeSettings = stripeOptions.Value;
        }

        public async Task<string> CreateCheckoutSessionAsync(int userId, int orderId, string domain)
        {
            var order = await _context.Orders
                .AsNoTracking() 
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            // Не раскрываем существование чужого заказа — отвечаем 404
            if (order == null || order.UserId != userId)
            {
                throw new KeyNotFoundException($"Заказ с ID {orderId} не найден.");
            }

            if (order.Status == OrderStatus.Paid)
            {
                throw new InvalidOperationException("Этот заказ уже оплачен.");
            }

            if (order.Status != OrderStatus.New)
            {
                throw new InvalidOperationException(
                    $"Оплатить можно только новый заказ. Текущий статус: '{order.Status}'.");
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
            var stripeEvent = EventUtility.ConstructEvent(json, stripeSignature, _stripeSettings.WebhookSecret);

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

            // Cancelled — терминальный статус: не «воскрешаем» отменённый заказ оплатой
            if (order == null || order.Status == OrderStatus.Paid || order.Status == OrderStatus.Cancelled)
            {
                return;
            }

            order.Status = OrderStatus.Paid;
            await _context.SaveChangesAsync();

            Console.WriteLine($"✅ Заказ #{orderId} успешно оплачен!");

            // Находим пользователя в базе по UserId из заказа
            var user = await _context.Users.FindAsync(order.UserId);
            
            // Отправляем email-чек пользователю через типизированный метод IEmailService                                                                     
            if (user != null && !string.IsNullOrEmpty(user.Email))                                                                                            
            {                                                                                                                                                 
                try                                                                                                                                           
                {                                                                                                                                             
                    await _emailService.SendPaymentReceiptAsync(order, user);                                                                                 
                    Console.WriteLine($"📧 Чек об оплате заказа #{order.Id} успешно отправлен на {user.Email}");                                              
                }                                                                                                                                             
                catch (Exception ex)                                                                                                                          
                {                                                                                                                                             
                    Console.WriteLine($"⚠️ Ошибка при отправке чека об оплате: {ex.Message}");                                                                
                }                                                                                                                                             
            }    
        }

        
    }
}
