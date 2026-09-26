using FluentValidation;

namespace Gym.Application.Cafe.CreateProduct;

public sealed class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(command => command.Name).ValidProductName();
        RuleFor(command => command.CategoryId).ValidCategoryId();
        RuleFor(command => command.Price).ValidProductPrice();
    }
}
