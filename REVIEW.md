# Разбор и план рефакторинга

## Файлы, которые, скорее всего, потребуют изменений

Ниже перечислены файлы, которые с наибольшей вероятностью придётся редактировать при выполнении запросов. Файлы, содержащие лишь контекст, в список не включены.

- `RayFluxMarket/Controllers/AuthController.cs`
- `RayFluxMarket/Controllers/CartController.cs`
- `RayFluxMarket/Controllers/OrdersController.cs`
- `RayFluxMarket/Controllers/PaymentsController.cs`
- `RayFluxMarket/Controllers/ProductsController.cs`
- `RayFluxMarket/Services/PaymentService.cs`
- `RayFluxMarket/Services/EmailService.cs`
- `RayFluxMarket/Data/AppDbContext.cs`
- `RayFluxMarket/Program.cs`

## План рефакторинга

1. **Вынести бизнес-логику из контроллеров в сервисы.**
   - `AuthController` — вынести генерацию токенов, регистрацию, логин, refresh и сброс пароля в `IAuthService`/`AuthService`.
   - `CartController` — вынести работу с корзиной в `ICartService`/`CartService`.
   - `OrdersController` — вынести оформление заказа, получение заказов и смену статуса в `IOrderService`/`OrderService`.
   - `PaymentsController` — вынести создание checkout-сессии и обработку webhook в `IPaymentService`.

2. **Устранить дублирование.**
   - Метод `GetCurrentUserId()` повторяется в `CartController` и `OrdersController` — вынести в базовый контроллер или extension-метод для `ClaimsPrincipal`.
   - Логика отправки писем (подтверждение заказа, чек об оплате, сброс пароля) — централизовать в `IEmailService` с типизированными методами.

3. **Улучшить работу с БД.**
   - Проверить использование `AsNoTracking()` в read-only запросах.
   - Убедиться, что транзакции при checkout корректно откатываются при ошибках.
   - Проверить корректность настройки связей и индексов в `AppDbContext.OnModelCreating`.

4. **Безопасность.**
   - Проверить срок жизни access/refresh токенов и их ротацию.
   - Убедиться, что эндпоинты изменения данных защищены `[Authorize(Roles = "Admin")]`.
   - Проверить валидацию входных DTO (FluentValidation) на всех эндпоинтах.

5. **Конфигурация и инфраструктура.**
   - Вынести настройки (JWT, Stripe, SMTP) в `IOptions<T>` с типизированными классами.
   - Проверить регистрацию зависимостей в `Program.cs` и корректность middleware-конвейера (в частности, `GlobalExceptionHandler`).

6. **Тестируемость.**
   - После выноса логики в сервисы добавить unit-тесты на ключевые сценарии (регистрация, checkout, оплата).
