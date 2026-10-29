using Gym.Application.SiteAuth;
using Gym.Domain.Members;
using Gym.Domain.SiteAuth;
using Gym.Domain.Trainers;

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
        // Postgres lets many rows hold NULL under a unique index, so trainer-only accounts fit.
        builder.HasIndex(account => account.MemberId)
            .IsUnique()
            .HasDatabaseName(SiteAuthConstraints.UniqueMember);

        // And a trainer, the same.
        builder.HasIndex(account => account.TrainerId)
            .IsUnique()
            .HasDatabaseName(SiteAuthConstraints.UniqueTrainer);

        // Members are deactivated, never deleted, so an account's member never disappears.
        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(account => account.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        // Trainers are stopped, never deleted, likewise.
        builder.HasOne<Trainer>()
            .WithMany()
            .HasForeignKey(account => account.TrainerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
