using Gym.Domain.SiteAuth;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>
/// The <c>sign_in_code_sends</c> table: every code SMS that may have cost money (BUSINESS_RULES.md
/// §15). What the per-phone and per-day limits count.
/// </summary>
public sealed class SignInCodeSendConfiguration : IEntityTypeConfiguration<SignInCodeSend>
{
    private const int PhoneMaxLength = 16;

    public void Configure(EntityTypeBuilder<SignInCodeSend> builder)
    {
        builder.ToTable("sign_in_code_sends", table =>
            table.HasCheckConstraint("ck_sign_in_code_sends_cost_not_negative", "cost_rial IS NULL OR cost_rial >= 0"));

        builder.Property(send => send.PhoneNumber).HasMaxLength(PhoneMaxLength).IsRequired();
        builder.Property(send => send.CostRial).HasPrecision(18, 2);

        // The per-phone limit: this phone's sends in the last hour.
        builder.HasIndex(send => new { send.PhoneNumber, send.SentAt });

        // The gym's limit: every send since the gym's midnight.
        builder.HasIndex(send => send.SentAt);
    }
}
