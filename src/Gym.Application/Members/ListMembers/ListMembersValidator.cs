using FluentValidation;

using Gym.Application.Common.Paging;
using Gym.Domain.Common.Text;
using Gym.Domain.Members;

namespace Gym.Application.Members.ListMembers;

public sealed class ListMembersValidator : AbstractValidator<ListMembersQuery>
{
    /// <summary>A shorter search matches almost every member.</summary>
    public const int SearchMinLength = 2;

    public ListMembersValidator()
    {
        RuleFor(query => query.Page).ValidPage();
        RuleFor(query => query.PageSize).ValidPageSize();

        // Blank is "no search", not "too short": clearing the search box lists everyone.
        When(query => !string.IsNullOrWhiteSpace(query.Search), () =>
        {
            // Measured after normalization: invisible marks and vowel marks are not characters
            // the search can match on.
            RuleFor(query => query.Search!)
                .Cascade(CascadeMode.Stop)
                .MaximumLength(Member.FullNameMaxLength)
                .WithErrorCode(MemberErrors.SearchTooLong.Code).WithMessage(MemberErrors.SearchTooLong.Description)
                .Must(search => PersianText.Normalize(search).Length >= SearchMinLength)
                .WithErrorCode(MemberErrors.SearchTooShort.Code).WithMessage(MemberErrors.SearchTooShort.Description);
        });
    }
}
