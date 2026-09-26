using FluentValidation;

namespace Gym.Application.Cafe.UpdateProduct;

public sealed class UpdateProductValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductValidator()
    {
        RuleFor(command => command.Name).ValidProductName();
        RuleFor(command => command.CategoryId).ValidCategoryId();
        RuleFor(command => command.Price).ValidProductPrice();
    }
}
