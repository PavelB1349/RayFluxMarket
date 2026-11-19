using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using System.Net;

namespace RayFluxMarket.Infrastructure
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            // 1. Логируем полную ошибку со стектрейсом в консоль/файл, чтобы разработчик видел, где бабахнуло
            _logger.LogError(exception, "Произошла необработанная ошибка: {Message}", exception.Message);

            // 2. Формируем красивый, стандартизированный ответ для клиента (RFC 7807 Problem Details)
            var problemDetails = new ProblemDetails
            {
                Status = (int)HttpStatusCode.InternalServerError,
                Title = "Внутренняя ошибка сервера",
                Detail = "На сервере произошла непредвиденная ошибка. Наша команда уже уведомлена и работает над исправлением."
            };

            // Маппинг доменных исключений в корректные HTTP-статусы
            switch (exception)
            {
                case UnauthorizedAccessException:
                    problemDetails.Status = (int)HttpStatusCode.Unauthorized;
                    problemDetails.Title = "Нет доступа";
                    problemDetails.Detail = exception.Message;
                    break;

                case KeyNotFoundException:
                    problemDetails.Status = (int)HttpStatusCode.NotFound;
                    problemDetails.Title = "Не найдено";
                    problemDetails.Detail = exception.Message;
                    break;

                case InvalidOperationException:
                    problemDetails.Status = (int)HttpStatusCode.BadRequest;
                    problemDetails.Title = "Некорректная операция";
                    problemDetails.Detail = exception.Message;
                    break;

                case StripeException stripeEx:
                    problemDetails.Status = (int)HttpStatusCode.BadRequest;
                    problemDetails.Title = "Ошибка платёжной системы";
                    problemDetails.Detail = stripeEx.Message;
                    break;

                case DbUpdateConcurrencyException:
                    problemDetails.Status = (int)HttpStatusCode.Conflict;
                    problemDetails.Title = "Конфликт данных";
                    problemDetails.Detail = "Данные были изменены другим запросом. Обновите страницу и попробуйте снова.";
                    break;

                case DbUpdateException:
                    problemDetails.Status = (int)HttpStatusCode.Conflict;
                    problemDetails.Title = "Конфликт данных";
                    problemDetails.Detail = "Не удалось сохранить изменения: нарушено ограничение целостности данных.";
                    break;
            }

            // 3. Настраиваем HTTP-ответ
            httpContext.Response.StatusCode = problemDetails.Status.Value;
            httpContext.Response.ContentType = "application/problem+json";

            // 4. Отправляем JSON клиенту
            await httpContext.Response
                .WriteAsJsonAsync(problemDetails, cancellationToken);

            return true; // Говорим .NET, что ошибка успешно обработана и конвейер можно останавливать
        }
    }
}
