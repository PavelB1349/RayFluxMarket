using FluentValidation;
using RayFluxMarket.Models.DTOs;

namespace RayFluxMarket.Validators
{
    // Наследуемся от AbstractValidator и указываем, какой DTO будем проверять
    public class RegisterDtoValidator : AbstractValidator<RegisterDto>
    {
        public RegisterDtoValidator()
        {
            // Правила для Email
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email не может быть пустым.")
                .EmailAddress().WithMessage("Введите корректный формат Email.");

            // Правила для Пароля
            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Пароль обязателен для заполнения.")
                .MinimumLength(6).WithMessage("Пароль должен содержать не менее 6 символов.")
                .Matches("[A-Z]").WithMessage("Пароль должен содержать хотя бы одну заглавную букву.")
                .Matches("[0-9]").WithMessage("Пароль должен содержать хотя бы одну цифру.");
        }
    }
}