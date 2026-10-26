using Gym.Application.Common.Security;
using Gym.Domain.SiteAuth;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>
/// The <c>sign_in_codes</c> table: one row per phone (BUSINESS_RULES.md §15). The rules live in
/// <see cref="SignInCode"/>; the database repeats the ones it can check.
/// </summary>
public sealed class SignInCodeConfiguration : IEntityTypeConfiguration<SignInCode>
{
    /// <summary>E.164 is at most 15 digits and the plus, as on members.</summary>
    private const int PhoneMaxLength = 16;

    public void Configure(EntityTypeBuilder<SignInCode> builder)
    {
        builder.ToTable("sign_in_codes", table =>
        {
            table.HasCheckConstraint(
                "ck_sign_in_codes_wrong_tries_in_range",
                $"wrong_tries >= 0 AND wrong_tries <= {SignInCode.MaxWrongTries}");
            table.HasCheckConstraint("ck_sign_in_codes_expires_after_issue", "expires_at > issued_at");
            table.HasCheckConstraint("ck_sign_in_codes_sent_after_issue", "last_sent_at >= issued_at");
            table.HasCheckConstraint("ck_sign_in_codes_used_after_issue", "used_at IS NULL OR used_at >= issued_at");
            table.HasCheckConstraint(
                "ck_sign_in_codes_wrong_tries_counted_with_their_day",
                "(wrong_tries_on IS NULL AND wrong_tries_that_day = 0) OR (wrong_tries_on IS NOT NULL AND wrong_tries_that_day > 0)");
        });

        builder.Property(code => code.PhoneNumber).HasMaxLength(PhoneMaxLength).IsRequired();

        // One row per phone: what keeps a phone from holding two codes at once.
        builder.HasIndex(code => code.PhoneNumber).IsUnique();

        builder.Property(code => code.CodeSeed).HasMaxLength(SignInCodeSeed.Length).IsRequired();

        builder.Property(code => code.Version).IsRowVersion();
    }
}
