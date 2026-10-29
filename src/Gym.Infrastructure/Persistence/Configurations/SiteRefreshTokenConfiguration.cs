using Gym.Domain.SiteAuth;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>
/// The <c>site_refresh_tokens</c> table: the same rules and the same database checks as the staff's
/// <c>refresh_tokens</c> (<see cref="RefreshTokenConfiguration"/>), in a table of its own (ADR 0010).
/// </summary>
public sealed class SiteRefreshTokenConfiguration : IEntityTypeConfiguration<SiteRefreshToken>
{
    /// <summary>SHA-256 as lower-case hex.</summary>
    private const int TokenHashLength = 64;

    public void Configure(EntityTypeBuilder<SiteRefreshToken> builder)
    {
        builder.ToTable("site_refresh_tokens", table =>
        {
            table.HasCheckConstraint(
                "ck_site_refresh_tokens_revocation_complete",
                "(revoked_at IS NULL) = (revoked_reason IS NULL)");

            table.HasCheckConstraint(
                "ck_site_refresh_tokens_replaced_only_when_rotated",
                "replaced_by_token_id IS NULL OR revoked_reason = 'Rotated'");
        });

        builder.Property(token => token.TokenHash)
            .HasMaxLength(TokenHashLength)
            .IsRequired();

        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasIndex(token => token.FamilyId);

        builder.Property(token => token.RevokedReason)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasOne<SiteRefreshToken>()
            .WithOne()
            .HasForeignKey<SiteRefreshToken>(token => token.ReplacedByTokenId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<SiteAccount>()
            .WithMany()
            .HasForeignKey(token => token.SiteAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(token => token.Version).IsRowVersion();
    }
}
