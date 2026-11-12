using Gym.Domain.Common;

namespace Gym.Domain.Shop;

public static class ShopErrors
{
    /// <summary>
    /// A contact way out of shape reached the entity. The validator and the handler check each one
    /// first under its own code, so this only appears if they ever disagree with the entity.
    /// </summary>
    public static readonly Error ContactInvalid = Error.Validation(
        "Shop.ContactInvalid",
        "A contact way is not in a valid shape.");

    public static readonly Error MobilePhoneInvalid = Error.Validation(
        "Shop.MobilePhoneInvalid",
        "The mobile number is not an Iranian mobile number.");

    public static readonly Error WhatsAppPhoneInvalid = Error.Validation(
        "Shop.WhatsAppPhoneInvalid",
        "The WhatsApp number is not an Iranian mobile number.");

    public static readonly Error ChangedConcurrently = Error.Conflict(
        "Shop.ChangedConcurrently",
        "The shop's settings were changed by someone else since they were read. Reload and try again.");
}
