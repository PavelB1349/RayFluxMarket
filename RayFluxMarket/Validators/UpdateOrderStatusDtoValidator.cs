using FluentValidation;
using RayFluxMarket.Models.DTOs;
using RayFluxMarket.Models.Enums;

namespace RayFluxMarket.Validators
{
    public class UpdateOrderStatusDtoValidator : AbstractValidator<UpdateOrderStatusDto>
    {
        public UpdateOrderStatusDtoValidator()
        {
            RuleFor(x => x.Status)
                .NotEmpty().WithMessage("Статус заказа обязателен.")
                .Must(BeAValidStatus).WithMessage("Недопустимый статус заказа.");
        }

        // Кастомное правило проверки
        private bool BeAValidStatus(string status)
        {
            var allowedStatuses = new List<string>
            {
                OrderStatus.New,
                OrderStatus.Processing,
                OrderStatus.Shipped,
                OrderStatus.Delivered,
                OrderStatus.Cancelled
            };

            return allowedStatuses.Contains(status);
        }
    }
}