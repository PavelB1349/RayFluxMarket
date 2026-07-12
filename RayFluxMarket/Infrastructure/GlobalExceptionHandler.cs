using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
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

            // Можно перехватывать конкретные ошибки и менять статус-код. Например:
            if (exception is UnauthorizedAccessException)
            {
                problemDetails.Status = (int)HttpStatusCode.Unauthorized;
                problemDetails.Title = "Нет доступа";
                problemDetails.Detail = exception.Message;
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