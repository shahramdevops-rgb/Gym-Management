using Gym.Application.Payables;
using Gym.Domain.Expenses;
using Gym.Domain.Payables;
using Gym.Infrastructure.Identity;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>
/// The <c>payables</c> table: cheques and instalments. The rules live in <see cref="Payable"/>; the
/// database repeats every one it can check. "A cheque is not paid before its date" is the one it
/// cannot: today depends on the gym's time zone and moves, and a check constraint has to hold for
/// the row forever.
/// </summary>
public sealed class PayableConfiguration : IEntityTypeConfiguration<Payable>
{
    public void Configure(EntityTypeBuilder<Payable> builder)
    {
        builder.ToTable("payables", table =>
        {
            table.HasCheckConstraint("ck_payables_kind", "kind IN ('Cheque', 'Installment')");
            table.HasCheckConstraint("ck_payables_amount_positive", "amount > 0");
            table.HasCheckConstraint("ck_payables_payee_not_blank", "btrim(payee) <> ''");
            table.HasCheckConstraint("ck_payables_description_not_blank", "btrim(description) <> ''");

            // «قسط n از N»: both numbers on an instalment, 1 <= n <= N <= 360; neither on a cheque.
            // The IS NOT NULLs are needed: a check that comes out NULL counts as passed in Postgres.
            table.HasCheckConstraint(
                "ck_payables_installment_numbers",
                "(kind <> 'Installment' AND installment_number IS NULL AND installment_count IS NULL) OR " +
                "(kind = 'Installment' AND installment_number IS NOT NULL AND installment_count IS NOT NULL " +
                "AND installment_number >= 1 AND installment_number <= installment_count " +
                $"AND installment_count <= {Payable.MaxInstallmentCount})");

            // Paid means a moment and a user; cancelled means a moment, a reason and a user. Never
            // one without the others, and never both (BUSINESS_RULES.md §9).
            table.HasCheckConstraint(
                "ck_payables_paid",
                "(paid_at IS NULL) = (paid_by_user_id IS NULL)");
            table.HasCheckConstraint(
                "ck_payables_cancelled",
                "(cancelled_at IS NULL) = (cancel_reason IS NULL) AND (cancelled_at IS NULL) = (cancelled_by_user_id IS NULL)");
            table.HasCheckConstraint(
                "ck_payables_not_paid_and_cancelled",
                "paid_at IS NULL OR cancelled_at IS NULL");
        });

        builder.Property(payable => payable.Kind).HasConversion<string>().HasMaxLength(20);

        // numeric(18,2): exact decimal arithmetic, never float (CLAUDE.md).
        builder.Property(payable => payable.Amount).HasPrecision(18, Payable.AmountDecimals);
        builder.Property(payable => payable.Payee).HasMaxLength(Payable.PayeeMaxLength).IsRequired();
        builder.Property(payable => payable.Description).HasMaxLength(Payable.DescriptionMaxLength).IsRequired();
        builder.Property(payable => payable.CancelReason).HasMaxLength(Payable.ReasonMaxLength);

        // Restrict: categories are never deleted (BUSINESS_RULES.md §9).
        builder.HasOne<ExpenseCategory>()
            .WithMany()
            .HasForeignKey(payable => payable.CategoryId)
            .HasConstraintName(PayableConstraints.CategoryForeignKey)
            .OnDelete(DeleteBehavior.Restrict);

        // Users are deactivated, never deleted, so whoever registered, paid or cancelled one can
        // never disappear.
        builder.HasOne<User>().WithMany().HasForeignKey(payable => payable.RegisteredByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(payable => payable.PaidByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(payable => payable.CancelledByUserId).OnDelete(DeleteBehavior.Restrict);

        // The register and the dashboard's reminder both read by date.
        builder.HasIndex(payable => payable.DueDate);
        builder.HasIndex(payable => payable.CategoryId);

        builder.Property(payable => payable.Version).IsRowVersion();
    }
}
