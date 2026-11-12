using FluentValidation;

using Gym.Domain.Shop;

namespace Gym.Application.Shop.UpdateShopContacts;

/// <summary>
/// The shapes from <see cref="ShopContactRules"/>, one per field, so the page shows each refusal under
/// its own field. The two mobiles are checked by the handler, which needs libphonenumber.
/// </summary>
public sealed class UpdateShopContactsValidator : AbstractValidator<UpdateShopContactsCommand>
{
    public UpdateShopContactsValidator()
    {
        RuleFor(command => ShopContactText.Landline(command.LandlinePhone))
            .Must(ShopContactRules.IsLandline)
            .WithErrorCode("Shop.LandlineInvalid")
            .WithMessage("The landline must be 11 digits with its area code, such as 07132345678.")
            .OverridePropertyName(nameof(UpdateShopContactsCommand.LandlinePhone));

        Handle(command => command.Telegram, nameof(UpdateShopContactsCommand.Telegram));
        Handle(command => command.Rubika, nameof(UpdateShopContactsCommand.Rubika));
        Handle(command => command.Eitaa, nameof(UpdateShopContactsCommand.Eitaa));
        Handle(command => command.Bale, nameof(UpdateShopContactsCommand.Bale));
        Handle(command => command.Instagram, nameof(UpdateShopContactsCommand.Instagram));
    }

    private void Handle(Func<UpdateShopContactsCommand, string?> field, string name) =>
        RuleFor(command => ShopContactText.Handle(field(command)))
            .Must(ShopContactRules.IsHandle)
            .WithErrorCode("Shop.HandleInvalid")
            .WithMessage("An id may hold only Latin letters, digits, _ and ., 3 to 32 of them.")
            .OverridePropertyName(name);
}
