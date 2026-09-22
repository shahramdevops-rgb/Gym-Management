using Gym.Application.Common;
using Gym.Application.Common.Paging;
using Gym.Domain.Common.Text;
using Gym.Domain.Members;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Members.ListMembers;

/// <summary>
/// The member list and the front-desk search box, as one paged query (BUSINESS_RULES.md §2).
/// </summary>
/// <remarks>
/// <para>
/// One box takes either a name or a phone number, so the handler decides which from what was
/// typed: only digits, <c>+</c>, spaces, dashes and brackets is a phone search; anything else
/// is a name search.
/// </para>
/// <para>
/// A name search compares the normalized search text with <c>NormalizedFullName</c>, so "علي"
/// typed on an Arabic keyboard finds "علی". A phone search normalizes a whole number exactly as
/// it was normalized when saved; four or more digits that are not a whole number match anywhere
/// in the stored number, because the front desk often knows only the last few digits.
/// </para>
/// <para>
/// Both searches are <c>LIKE '%…%'</c>. An ordinary index cannot help a pattern that starts with
/// a wildcard; the trigram (GIN) indexes in <c>MemberConfiguration</c> can.
/// </para>
/// </remarks>
public sealed class ListMembersHandler(IAppDbContext db, IPhoneNormalizer phones)
{
    /// <summary>Fewer digits than this would match a large share of all numbers.</summary>
    public const int PhoneFragmentMinDigits = 4;

    private const string LikeEscape = "\\";

    public async Task<PagedResponse<MemberResponse>> Handle(ListMembersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var members = db.Members.AsNoTracking();

        if (query.IsActive is { } isActive)
        {
            members = members.Where(member => member.IsActive == isActive);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            members = Search(members, PersianText.Normalize(query.Search));
        }

        var totalCount = await members.CountAsync(cancellationToken);

        // Name first for people; Id breaks ties so two members with the same name keep their
        // order from one page to the next and none is shown twice or skipped.
        var pageMembers = await members
            .OrderBy(member => member.NormalizedFullName)
            .ThenBy(member => member.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(MemberResponse.Projection)
            .ToListAsync(cancellationToken);

        // Batched for the whole page (MemberDebt), not one query per row.
        var debtByMemberId = await MemberDebt.GetTotalsAsync(
            db, pageMembers.Select(member => member.Id).ToList(), cancellationToken);

        var items = pageMembers
            .Select(member => member with { Debt = debtByMemberId.GetValueOrDefault(member.Id) })
            .ToList();

        return new PagedResponse<MemberResponse>(items, query.Page, query.PageSize, totalCount);
    }

    private IQueryable<Member> Search(IQueryable<Member> members, string text)
    {
        if (!LooksLikePhone(text))
        {
            // The column is stored lower-cased, so lower-casing the input makes the search
            // case-insensitive for Latin names without a function on the column.
            var pattern = $"%{EscapeLike(text.ToLowerInvariant())}%";

            return members.Where(member => EF.Functions.Like(member.NormalizedFullName, pattern, LikeEscape));
        }

        var phone = phones.Normalize(text);
        if (phone.IsSuccess)
        {
            return members.Where(member => member.PhoneNumber == phone.Value);
        }

        // A fragment. Leading zeros are the trunk prefix ("0912…", "0098…"), which E.164 does not
        // store, so they are dropped: "0912 123" then finds "+98912123….".
        var digits = new string(text.Where(char.IsAsciiDigit).ToArray());
        var significant = digits.TrimStart('0');
        if (digits.Length < PhoneFragmentMinDigits || significant.Length == 0)
        {
            return members.Where(_ => false);
        }

        // Digits only, so nothing in the pattern needs escaping.
        return members.Where(member => EF.Functions.Like(member.PhoneNumber, $"%{significant}%"));
    }

    private static bool LooksLikePhone(string text) =>
        text.Any(char.IsAsciiDigit) && text.All(c => char.IsAsciiDigit(c) || c is '+' or ' ' or '-' or '(' or ')');

    /// <summary>A typed <c>%</c> or <c>_</c> is a character to find, not a wildcard.</summary>
    private static string EscapeLike(string value) =>
        value.Replace(LikeEscape, LikeEscape + LikeEscape, StringComparison.Ordinal)
            .Replace("%", LikeEscape + "%", StringComparison.Ordinal)
            .Replace("_", LikeEscape + "_", StringComparison.Ordinal);
}
