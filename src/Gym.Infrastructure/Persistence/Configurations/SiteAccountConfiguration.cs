using Gym.Application.SiteAuth;
using Gym.Domain.Members;
using Gym.Domain.SiteAuth;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>
/// The <c>site_accounts</c> table (BUSINESS_RULES.md §15, ADR 0010). Apart from Identity's users:
/// a site account is never a staff user.
/// </summary>
public sealed class SiteAccountConfiguration : IEntityTypeConfiguration<SiteAccount>
{
    public void Configure(EntityTypeBuilder<SiteAccount> builder)
    {
        builder.ToTable("site_accounts");

        // A member has one account at most: linking twice would split their sessions in two.
        builder.HasIndex(account => account.MemberId)
            .IsUnique()
            .HasDatabaseName(SiteAuthConstraints.UniqueMember);

        // Members are deactivated, never deleted, so an account's member never disappears.
        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(account => account.MemberId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
