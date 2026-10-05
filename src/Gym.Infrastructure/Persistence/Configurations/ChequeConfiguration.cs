using Gym.Domain.Cheques;
using Gym.Infrastructure.Identity;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>
/// The <c>cheques</c> table. The rules live in <see cref="Cheque"/>; the database repeats every
/// one it can check. "Not passed before its date" is the one it cannot: today depends on the gym's
/// time zone and moves, and a check constraint has to hold for the row forever.
/// </summary>
public sealed class ChequeConfiguration : IEntityTypeConfiguration<Cheque>
{
    public void Configure(EntityTypeBuilder<Cheque> builder)
    {
        builder.ToTable("cheques", table =>
        {
            table.HasCheckConstraint("ck_cheques_amount_positive", "amount > 0");
            table.HasCheckConstraint("ck_cheques_payee_not_blank", "btrim(payee) <> ''");
            table.HasCheckConstraint("ck_cheques_description_not_blank", "btrim(description) <> ''");

            // Passed means a moment and a user; cancelled means a moment, a reason and a user. Never
            // one without the others, and never both: each one is final (BUSINESS_RULES.md §9).
            table.HasCheckConstraint(
                "ck_cheques_passed",
                "(passed_at IS NULL) = (passed_by_user_id IS NULL)");
            table.HasCheckConstraint(
                "ck_cheques_cancelled",
                "(cancelled_at IS NULL) = (cancel_reason IS NULL) AND (cancelled_at IS NULL) = (cancelled_by_user_id IS NULL)");
            table.HasCheckConstraint(
                "ck_cheques_not_passed_and_cancelled",
                "passed_at IS NULL OR cancelled_at IS NULL");
        });

        // numeric(18,2): exact decimal arithmetic, never float (CLAUDE.md).
        builder.Property(cheque => cheque.Amount).HasPrecision(18, Cheque.AmountDecimals);
        builder.Property(cheque => cheque.Payee).HasMaxLength(Cheque.PayeeMaxLength).IsRequired();
        builder.Property(cheque => cheque.Description).HasMaxLength(Cheque.DescriptionMaxLength).IsRequired();
        builder.Property(cheque => cheque.CancelReason).HasMaxLength(Cheque.CancelReasonMaxLength);

        // Users are deactivated, never deleted, so whoever registered, passed or cancelled a cheque
        // can never disappear.
        builder.HasOne<User>().WithMany().HasForeignKey(cheque => cheque.RegisteredByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(cheque => cheque.PassedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(cheque => cheque.CancelledByUserId).OnDelete(DeleteBehavior.Restrict);

        // The register and the dashboard's reminder both read cheques by date.
        builder.HasIndex(cheque => cheque.DueDate);

        builder.Property(cheque => cheque.Version).IsRowVersion();
    }
}
