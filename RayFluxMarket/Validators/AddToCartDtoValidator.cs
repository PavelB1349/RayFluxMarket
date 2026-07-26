using FluentValidation;
using RayFluxMarket.Models.DTOs;

namespace RayFluxMarket.Validators
{
    public class AddToCartDtoValidator : AbstractValidator<AddToCartDto>
    {
        public AddToCartDtoValidator()
        {
            RuleFor(x => x.ProductId)
                .GreaterThan(0).WithMessage("Некорректный ID товара.");

            RuleFor(x => x.Quantity)
                .GreaterThan(0).WithMessage("Количество товара должно быть больше нуля.")
                .LessThanOrEqualTo(100).WithMessage("Нельзя добавить более 100 единиц одного товара за раз.");
        }
    }
}