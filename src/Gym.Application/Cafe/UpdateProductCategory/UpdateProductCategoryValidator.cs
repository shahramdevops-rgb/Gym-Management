using FluentValidation;

namespace Gym.Application.Cafe.UpdateProductCategory;

public sealed class UpdateProductCategoryValidator : AbstractValidator<UpdateProductCategoryCommand>
{
    public UpdateProductCategoryValidator()
    {
        RuleFor(command => command.Name).ValidCategoryName();
    }
}
