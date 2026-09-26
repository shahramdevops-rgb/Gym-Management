using Gym.Domain.Auth;
using Gym.Infrastructure.Identity;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>
/// The <c>trusted_devices</c> table. The rules live in <see cref="TrustedDevice"/> and
/// <c>UserAuthenticator</c>; the database repeats the ones it can check.
/// </summary>
public sealed class TrustedDeviceConfiguration : IEntityTypeConfiguration<TrustedDevice>
{
    /// <summary>SHA-256 as lower-case hex.</summary>
    private const int TokenHashLength = 64;

    public void Configure(EntityTypeBuilder<TrustedDevice> builder)
    {
        builder.ToTable("trusted_devices", table =>
        {
            table.HasCheckConstraint("ck_trusted_devices_failed_attempts_not_negative", "failed_attempts >= 0");
            table.HasCheckConstraint("ck_trusted_devices_expires_after_last_use", "expires_at > last_used_at");
        });

        builder.Property(device => device.TokenHash)
            .HasMaxLength(TokenHashLength)
            .IsRequired();

        // One row per person per browser. Login looks a device up by both, and two rows for the
        // same pair would split its count of wrong passwords in two.
        builder.HasIndex(device => new { device.UserId, device.TokenHash }).IsUnique();

        // Login asks "has this cookie been trusted for anyone?" before reusing its secret.
        builder.HasIndex(device => device.TokenHash);

        // Users are deactivated, never deleted, so a user with devices can never disappear.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(device => device.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
