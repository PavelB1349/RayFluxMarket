using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RayFluxMarket.Extensions;
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
        [Authorize] // платёжную сессию может создать только авторизованный пользователь
        public async Task<IActionResult> CreateCheckoutSession(int orderId)
        {
            // Берём Origin запроса из React-приложения, а если его нет — используем Host или схему по умолчанию.
            // Referer содержит полный адрес страницы, поэтому из него нужно выделить только схему и хост,
            // иначе SuccessUrl/CancelUrl получатся битыми (https://site/cart/checkout-success).
            string? referer = Request.Headers["Referer"].FirstOrDefault();
            string? refererOrigin = Uri.TryCreate(referer, UriKind.Absolute, out var refererUri)
                ? refererUri.GetLeftPart(UriPartial.Authority)
                : null;

            string domain = (Request.Headers["Origin"].FirstOrDefault()
                             ?? refererOrigin
                             ?? _configuration["FrontendUrl"]
                             ?? $"{Request.Scheme}://{Request.Host}").TrimEnd('/');

            int userId = User.GetUserId();
            string checkoutUrl = await _paymentService.CreateCheckoutSessionAsync(userId, orderId, domain);
            return Ok(new { url = checkoutUrl });
        }

        // POST: api/Payments/webhook
        [HttpPost("webhook")]
        [AllowAnonymous] // Stripe не передаёт JWT — вебхук должен быть доступен без авторизации
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
