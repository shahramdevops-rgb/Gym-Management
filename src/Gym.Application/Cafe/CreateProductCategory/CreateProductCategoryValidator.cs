using FluentValidation;

namespace Gym.Application.Cafe.CreateProductCategory;

public sealed class CreateProductCategoryValidator : AbstractValidator<CreateProductCategoryCommand>
{
    public CreateProductCategoryValidator()
    {
        RuleFor(command => command.Name).ValidCategoryName();
    }
}
