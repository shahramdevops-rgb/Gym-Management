using Gym.Domain.Common.Text;

namespace Gym.Application.Shop.UpdateShopContacts;

/// <summary>
/// Cleans what was typed into a contact field, the same way for the validator and the handler, so the
/// value checked is the value saved.
/// </summary>
public static class ShopContactText
{
    /// <summary>Persian and Arabic digits made Latin, every space, line break and dash removed; empty is null.</summary>
    public static string? Landline(string? typed)
    {
        if (string.IsNullOrWhiteSpace(typed))
        {
            return null;
        }

        var digits = string.Concat(PersianText.NormalizeDigits(typed).Where(c => !char.IsWhiteSpace(c) && c != '-'));

        return digits.Length == 0 ? null : digits;
    }

    /// <summary>Trimmed and without a leading <c>@</c>; empty is null.</summary>
    public static string? Handle(string? typed)
    {
        if (string.IsNullOrWhiteSpace(typed))
        {
            return null;
        }

        var handle = typed.Trim().TrimStart('@');

        return handle.Length == 0 ? null : handle;
    }
}
