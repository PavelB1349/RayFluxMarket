using Microsoft.AspNetCore.Mvc;
using RayFluxMarket.Services;
using Stripe;

namespace RayFluxMarket.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly IConfiguration _configuration;

        public PaymentsController(IPaymentService paymentService, IConfiguration configuration)
        {
            _paymentService = paymentService;
            _configuration = configuration;
        }

        // POST: api/Payments/create-checkout-session/5
        [HttpPost("create-checkout-session/{orderId}")]
        public async Task<IActionResult> CreateCheckoutSession(int orderId)
        {
            // Берём Origin запроса из React-приложения, а если его нет — используем Host или схему по умолчанию
            string domain = Request.Headers["Origin"].FirstOrDefault()
                            ?? Request.Headers["Referer"].FirstOrDefault()?.TrimEnd('/')
                            ?? _configuration["FrontendUrl"]
                            ?? $"{Request.Scheme}://{Request.Host}";

            string checkoutUrl = await _paymentService.CreateCheckoutSessionAsync(orderId, domain);
            return Ok(new { url = checkoutUrl });
        }

        // POST: api/Payments/webhook
        [HttpPost("webhook")]
        public async Task<IActionResult> StripeWebhook()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            var signature = Request.Headers["Stripe-Signature"].ToString();

            try
            {
                await _paymentService.HandleWebhookAsync(json, signature);
                return Ok();
            }
            catch (StripeException e)
            {
                return BadRequest(new { message = "Ошибка валидации вебхука", error = e.Message });
            }
        }

    }
}
