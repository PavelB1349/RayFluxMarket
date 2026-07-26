using FluentValidation;
using RayFluxMarket.Models.DTOs;

namespace RayFluxMarket.Validators
{
    public class LoginDtoValidator : AbstractValidator<LoginDto>
    {
        public LoginDtoValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email обязателен для заполнения.")
                .EmailAddress().WithMessage("Некорректный формат Email.");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Пароль обязателен для заполнения.");
        }
    }
}