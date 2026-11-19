using FluentValidation;
using RayFluxMarket.Models.DTOs;

namespace RayFluxMarket.Validators
{
    public class CheckoutDtoValidator : AbstractValidator<CheckoutDto>
    {
        public CheckoutDtoValidator()
        {
            RuleFor(x => x.CustomerName)
                .NotEmpty().WithMessage("Имя обязательно.")
                .MaximumLength(100).WithMessage("Имя не должно превышать 100 символов.");

            RuleFor(x => x.PhoneNumber)
                .NotEmpty().WithMessage("Телефон обязателен.")
                .Matches(@"^\+?[0-9\s\-\(\)]{7,20}$").WithMessage("Некорректный формат телефона.");

            RuleFor(x => x.ShippingAddress)
                .NotEmpty().WithMessage("Адрес доставки обязателен.")
                .MaximumLength(300).WithMessage("Адрес не должен превышать 300 символов.");

            RuleFor(x => x.Comment)
                .MaximumLength(500).WithMessage("Комментарий не должен превышать 500 символов.");
        }
    }
}
