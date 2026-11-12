using FluentValidation;

namespace Gym.Application.Shop.RenameShopCategory;

public sealed class RenameShopCategoryValidator : AbstractValidator<RenameShopCategoryCommand>
{
    public RenameShopCategoryValidator()
    {
        RuleFor(command => command.Name).ValidShopCategoryName();
    }
}
