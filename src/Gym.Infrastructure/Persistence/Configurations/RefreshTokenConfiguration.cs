using Gym.Domain.Auth;
using Gym.Infrastructure.Identity;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>
/// The <c>refresh_tokens</c> table. The rules live in <see cref="RefreshToken"/>; the database
/// repeats the ones it can check, so a bug in the application cannot break them.
/// </summary>
public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    /// <summary>SHA-256 as lower-case hex.</summary>
    private const int TokenHashLength = 64;

    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens", table =>
        {
            // Revoked means both "when" and "why" are known; one without the other is a bug.
            table.HasCheckConstraint(
                "ck_refresh_tokens_revocation_complete",
                "(revoked_at IS NULL) = (revoked_reason IS NULL)");

            // Only rotation creates a child, so only a rotated token may point at one.
            table.HasCheckConstraint(
                "ck_refresh_tokens_replaced_only_when_rotated",
                "replaced_by_token_id IS NULL OR revoked_reason = 'Rotated'");
        });

        builder.Property(token => token.TokenHash)
            .HasMaxLength(TokenHashLength)
            .IsRequired();

        // Looking a token up by its hash is the one query every refresh runs, and two tokens
        // with the same hash would make that lookup ambiguous.
        builder.HasIndex(token => token.TokenHash).IsUnique();

        // Revoking a family loads every token in it.
        builder.HasIndex(token => token.FamilyId);

        builder.Property(token => token.RevokedReason)
            .HasConversion<string>()
            .HasMaxLength(20);

        // A token has at most one child, and it must be a real token. Unique, so two
        // concurrent rotations cannot both record a replacement for the same parent.
        builder.HasOne<RefreshToken>()
            .WithOne()
            .HasForeignKey<RefreshToken>(token => token.ReplacedByTokenId)
            .OnDelete(DeleteBehavior.Restrict);

        // Users are deactivated, never deleted, so a user with tokens can never disappear.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(token => token.Version).IsRowVersion();
    }
}
