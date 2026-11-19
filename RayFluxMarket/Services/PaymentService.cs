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

            // Брошенная сессия: пользователь не оплатил — отменяем заказ и возвращаем остатки
            if (stripeEvent.Type == "checkout.session.expired")
            {
                if (stripeEvent.Data.Object is Session expiredSession &&
                    expiredSession.Metadata.TryGetValue("OrderId", out string? expiredOrderIdStr) &&
                    int.TryParse(expiredOrderIdStr, out int expiredOrderId))
                {
                    await CancelExpiredOrderAsync(expiredOrderId);
                }
                return;
            }

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

            if (order == null)
            {
                return;
            }

            // Идемпотентность: повторная доставка вебхука не должна ничего менять
            if (order.Status == OrderStatus.Paid)
            {
                return;
            }

            // Cancelled — терминальный статус: заказ не «воскрешаем».
            // Но если деньги всё же списаны, их нужно вернуть покупателю.
            if (order.Status == OrderStatus.Cancelled)
            {
                await RefundCancelledOrderAsync(order, session);
                return;
            }

            order.Status = OrderStatus.Paid;
            order.StripePaymentIntentId = session.PaymentIntentId;
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

        public async Task RefundOrderAsync(int orderId)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null)
            {
                throw new KeyNotFoundException($"Заказ с ID {orderId} не найден.");
            }

            if (string.IsNullOrEmpty(order.StripePaymentIntentId))
            {
                // Заказ оплачен вне Stripe (например, вручную) — возврат не требуется
                return;
            }

            var refundService = new RefundService();
            try
            {
                var refund = await refundService.CreateAsync(
                    new RefundCreateOptions
                    {
                        PaymentIntent = order.StripePaymentIntentId,
                        Reason = "requested_by_customer",
                        Metadata = new Dictionary<string, string> { { "OrderId", order.Id.ToString() } }
                    },
                    // Идемпотентность: повторный вызов не создаст второй возврат
                    new RequestOptions { IdempotencyKey = $"refund-order-{order.Id}" });

                Console.WriteLine(
                    $"↩️ По заказу #{order.Id} оформлен возврат {refund.Amount / 100m:N2} {refund.Currency} (refund {refund.Id}).");
            }
            catch (StripeException ex)
            {
                Console.WriteLine($"⚠️ Не удалось оформить возврат по заказу #{order.Id}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Оформляет возврат средств по отменённому заказу, если оплата всё же прошла.
        /// </summary>
        private async Task RefundCancelledOrderAsync(Order order, Session session)
        {
            if (string.IsNullOrEmpty(session.PaymentIntentId))
            {
                Console.WriteLine(
                    $"⚠️ Заказ #{order.Id} отменён, но в сессии Stripe нет PaymentIntentId — автоматический возврат невозможен.");
                return;
            }

            // Сохраняем PaymentIntentId, чтобы RefundOrderAsync мог выполнить возврат
            order.StripePaymentIntentId = session.PaymentIntentId;
            await _context.SaveChangesAsync();

            await RefundOrderAsync(order.Id);
        }

        /// <summary>
        /// Отменяет заказ, по которому истекла Stripe-сессия, и возвращает остатки на склад.
        /// </summary>
        private async Task CancelExpiredOrderAsync(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            // Отменяем только «висящие» новые заказы — оплаченные/отменённые не трогаем
            if (order == null || order.Status != OrderStatus.New)
            {
                return;
            }

            foreach (var item in order.OrderItems)
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product != null)
                {
                    product.StockQuantity += item.Quantity;
                }
            }

            order.Status = OrderStatus.Cancelled;
            await _context.SaveChangesAsync();

            Console.WriteLine($"⏱️ Заказ #{orderId} отменён: истекла сессия оплаты Stripe.");
        }
    }
}
