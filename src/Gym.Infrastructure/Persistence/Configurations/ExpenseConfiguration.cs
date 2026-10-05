using Gym.Application.Expenses;
using Gym.Application.Payables;
using Gym.Domain.Expenses;
using Gym.Domain.Payables;
using Gym.Infrastructure.Identity;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>
/// The <c>expenses</c> table. The rules live in <see cref="Expense"/>; the database repeats every
/// one it can check. "Not after today" is the one it cannot: today depends on the gym's time zone
/// and moves, and a check constraint has to hold for the row forever.
/// </summary>
public sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("expenses", table =>
        {
            table.HasCheckConstraint("ck_expenses_amount_positive", "amount > 0");
            table.HasCheckConstraint("ck_expenses_description_not_blank", "btrim(description) <> ''");

            // Voided means a moment, a reason and a user, never one without the others — the same
            // pairing ServiceChargeConfiguration enforces for a void.
            table.HasCheckConstraint(
                "ck_expenses_void",
                "(voided_at IS NULL) = (void_reason IS NULL) AND (voided_at IS NULL) = (voided_by_user_id IS NULL)");
        });

        // numeric(18,2): exact decimal arithmetic, never float (CLAUDE.md).
        builder.Property(expense => expense.Amount).HasPrecision(18, Expense.AmountDecimals);
        builder.Property(expense => expense.Description).HasMaxLength(Expense.DescriptionMaxLength).IsRequired();
        builder.Property(expense => expense.ReferenceNumber).HasMaxLength(Expense.ReferenceNumberMaxLength);
        builder.Property(expense => expense.VoidReason).HasMaxLength(Expense.VoidReasonMaxLength);

        // Restrict: categories are never deleted (BUSINESS_RULES.md §9), and if one ever were, it
        // must not take the gym's spending history with it.
        builder.HasOne<ExpenseCategory>()
            .WithMany()
            .HasForeignKey(expense => expense.CategoryId)
            .HasConstraintName(ExpenseConstraints.CategoryForeignKey)
            .OnDelete(DeleteBehavior.Restrict);

        // Users are deactivated, never deleted, so a user who recorded or voided an expense can
        // never disappear.
        builder.HasOne<User>().WithMany().HasForeignKey(expense => expense.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(expense => expense.VoidedByUserId).OnDelete(DeleteBehavior.Restrict);

        // Restrict: a cheque or instalment is never deleted, and its expense must not outlive it.
        builder.HasOne<Payable>().WithMany().HasForeignKey(expense => expense.PayableId).OnDelete(DeleteBehavior.Restrict);

        // At most one standing expense per cheque or instalment (BUSINESS_RULES.md §9 *Cheques and
        // instalments*): a payment sent back to pending leaves a voided one, and paying it again
        // writes a new one, so the index is partial. It also settles two «پرداخت شد» racing.
        builder.HasIndex(expense => expense.PayableId)
            .IsUnique()
            .HasFilter("voided_at IS NULL")
            .HasDatabaseName(PayableConstraints.OneStandingExpense);

        // The list and the Phase 9 reports both filter by date range, and by category within it.
        builder.HasIndex(expense => new { expense.ExpenseDate, expense.CategoryId });
        builder.HasIndex(expense => expense.CategoryId);

        builder.Property(expense => expense.Version).IsRowVersion();
    }
}
