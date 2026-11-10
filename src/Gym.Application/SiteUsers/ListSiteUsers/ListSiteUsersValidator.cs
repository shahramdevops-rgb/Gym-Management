using FluentValidation;

using Gym.Application.Common.Paging;
using Gym.Application.Members.ListMembers;
using Gym.Domain.Common.Text;
using Gym.Domain.Members;

namespace Gym.Application.SiteUsers.ListSiteUsers;

/// <summary>The same limits as the member list's search, since it is the same box.</summary>
public sealed class ListSiteUsersValidator : AbstractValidator<ListSiteUsersQuery>
{
    public ListSiteUsersValidator()
    {
        RuleFor(query => query.Page).ValidPage();
        RuleFor(query => query.PageSize).ValidPageSize();

        When(query => !string.IsNullOrWhiteSpace(query.Search), () =>
        {
            RuleFor(query => query.Search!)
                .Cascade(CascadeMode.Stop)
                .MaximumLength(Member.FullNameMaxLength)
                .WithErrorCode(MemberErrors.SearchTooLong.Code).WithMessage(MemberErrors.SearchTooLong.Description)
                .Must(search => PersianText.Normalize(search).Length >= ListMembersValidator.SearchMinLength)
                .WithErrorCode(MemberErrors.SearchTooShort.Code).WithMessage(MemberErrors.SearchTooShort.Description);
        });
    }
}
