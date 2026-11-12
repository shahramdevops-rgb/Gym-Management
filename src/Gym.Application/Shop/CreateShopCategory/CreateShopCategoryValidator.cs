using FluentValidation;

namespace Gym.Application.Shop.CreateShopCategory;

public sealed class CreateShopCategoryValidator : AbstractValidator<CreateShopCategoryCommand>
{
    public CreateShopCategoryValidator()
    {
        RuleFor(command => command.Name).ValidShopCategoryName();
    }
}
