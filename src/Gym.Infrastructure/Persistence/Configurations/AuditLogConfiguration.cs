using Gym.Domain.Audit;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>
/// The <c>audit_logs</c> table. The append-only trigger is created in the migration itself,
/// because EF Core has no model API for triggers.
/// </summary>
public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs", table =>
        {
            table.HasCheckConstraint("ck_audit_logs_action", "action IN ('Insert', 'Update', 'Delete')");

            // An insert has nothing before it, a delete nothing after it, an update both.
            table.HasCheckConstraint(
                "ck_audit_logs_values_match_action",
                "(action = 'Insert' AND old_values IS NULL AND new_values IS NOT NULL) OR " +
                "(action = 'Update' AND old_values IS NOT NULL AND new_values IS NOT NULL) OR " +
                "(action = 'Delete' AND old_values IS NOT NULL AND new_values IS NULL)");
        });

        builder.Property(log => log.Action).HasConversion<string>().HasMaxLength(10);
        builder.Property(log => log.EntityType).HasMaxLength(100);
        builder.Property(log => log.EntityId).HasMaxLength(100);

        // jsonb, so the audit screen (task 11.1) can query inside the values if it needs to.
        builder.Property(log => log.OldValues).HasColumnType("jsonb");
        builder.Property(log => log.NewValues).HasColumnType("jsonb");

        // An IPv6 address is at most 45 characters as text.
        builder.Property(log => log.IpAddress).HasMaxLength(45);

        // The questions the audit screen asks: the history of one record, what one user did,
        // and what happened in a period.
        builder.HasIndex(log => new { log.EntityType, log.EntityId });
        builder.HasIndex(log => log.UserId);
        builder.HasIndex(log => log.OccurredAt);
    }
}
