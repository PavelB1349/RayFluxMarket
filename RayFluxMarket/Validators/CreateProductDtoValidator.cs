using FluentValidation;
using RayFluxMarket.Models.DTOs;

namespace RayFluxMarket.Validators
{
    public class CreateProductDtoValidator : AbstractValidator<CreateProductDto>
    {
        public CreateProductDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Название товара обязательно.")
                .MaximumLength(150).WithMessage("Название не должно превышать 150 символов.");

            RuleFor(x => x.Description)
                .NotEmpty().WithMessage("Описание товара обязательно.")
                .MaximumLength(1000).WithMessage("Описание не должно превышать 1000 символов.");

            RuleFor(x => x.Price)
                .GreaterThan(0).WithMessage("Цена товара должна быть больше 0.");

            RuleFor(x => x.CategoryId)
                .GreaterThan(0).WithMessage("Необходимо указать корректную категорию.");

            RuleFor(x => x.BrandId)
                .GreaterThan(0).WithMessage("Необходимо указать корректный бренд.");

            // Коллекция и сезон могут быть необязательными, но если они есть — ограничиваем длину
            RuleFor(x => x.Season)
                .MaximumLength(50).WithMessage("Сезон не должен превышать 50 символов.");

            RuleFor(x => x.Collection)
                .MaximumLength(100).WithMessage("Название коллекции не должно превышать 100 символов.");
        }
    }
}