using FluentValidation;
using RayFluxMarket.Models.DTOs;

namespace RayFluxMarket.Validators
{
    public class ResetPasswordDtoValidator : AbstractValidator<ResetPasswordDto>
    {
        public ResetPasswordDtoValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email обязателен.")
                .EmailAddress().WithMessage("Некорректный формат Email.");

            RuleFor(x => x.Token)
                .NotEmpty().WithMessage("Код сброса пароля обязателен.");

            RuleFor(x => x.NewPassword)
                .NotEmpty().WithMessage("Новый пароль обязателен для заполнения.")
                .MinimumLength(6).WithMessage("Пароль должен содержать не менее 6 символов.")
                .Matches("[A-Z]").WithMessage("Пароль должен содержать хотя бы одну заглавную букву.")
                .Matches("[0-9]").WithMessage("Пароль должен содержать хотя бы одну цифру.");
        }
    }
}