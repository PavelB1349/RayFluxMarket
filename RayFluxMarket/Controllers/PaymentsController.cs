using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RayFluxMarket.Data;
using RayFluxMarket.Models.Entities;
using RayFluxMarket.Models.Enums;
using RayFluxMarket.Services;
using Stripe;
using Stripe.Checkout;
using System.IO;
using System.Text;

namespace RayFluxMarket.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IPaymentService _paymentService;
        private readonly IEmailService _emailService;
        private readonly string _webhookSecret;

        public PaymentsController(
            AppDbContext context,
            IPaymentService paymentService,
            IEmailService emailService,
            IConfiguration configuration)
        {
            _context = context;
            _paymentService = paymentService;
            _emailService = emailService;
            _webhookSecret = configuration["Stripe:WebhookSecret"];
        }

        // POST: api/Payments/create-checkout-session/5
        [HttpPost("create-checkout-session/{orderId}")]
        public async Task<IActionResult> CreateCheckoutSession(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                return NotFound(new { message = $"Заказ с ID {orderId} не найден." });
            }

            if (order.Status == OrderStatus.Paid.ToString())
            {
                return BadRequest(new { message = "Этот заказ уже оплачен." });
            }

            string domain = $"{Request.Scheme}://{Request.Host}";

            try
            {
                string checkoutUrl = await _paymentService.CreateCheckoutSessionAsync(order, domain);
                return Ok(new { url = checkoutUrl });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Ошибка при создании платежной сессии Stripe", error = ex.Message });
            }
        }

        // POST: api/Payments/webhook
        [HttpPost("webhook")]
        public async Task<IActionResult> StripeWebhook()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

            try
            {
                var stripeEvent = EventUtility.ConstructEvent(
                    json,
                    Request.Headers["Stripe-Signature"],
                    _webhookSecret
                );

                if (stripeEvent.Type == "checkout.session.completed")
                {
                    var session = stripeEvent.Data.Object as Session;

                    if (session != null && session.Metadata.TryGetValue("OrderId", out string orderIdStr))
                    {
                        if (int.TryParse(orderIdStr, out int orderId))
                        {
                            // Подгружаем заказ вместе с пользователем и позициями товара
                            var order = await _context.Orders
                                .Include(o => o.OrderItems)
                                .ThenInclude(oi => oi.Product)
                                .FirstOrDefaultAsync(o => o.Id == orderId);

                            if (order != null && order.Status != OrderStatus.Paid.ToString())
                            {
                                order.Status = OrderStatus.Paid.ToString();
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
                        }
                    }
                }

                return Ok();
            }
            catch (StripeException e)
            {
                return BadRequest(new { message = "Ошибка валидации вебхука", error = e.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Внутренняя ошибка", error = ex.Message });
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