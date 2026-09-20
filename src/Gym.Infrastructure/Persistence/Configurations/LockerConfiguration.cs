using Gym.Application.Lockers;
using Gym.Domain.Lockers;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>
/// The <c>lockers</c> table. The rules live in <see cref="Locker"/>; the database repeats every
/// one it can check, so a bug or a hand-written SQL statement cannot store a locker the app
/// would reject.
/// </summary>
public sealed class LockerConfiguration : IEntityTypeConfiguration<Locker>
{
    public void Configure(EntityTypeBuilder<Locker> builder)
    {
        builder.ToTable("lockers", table =>
            table.HasCheckConstraint("ck_lockers_number_positive", $"number >= {Locker.MinNumber}"));

        builder.HasIndex(locker => locker.Number)
            .IsUnique()
            .HasDatabaseName(LockerConstraints.UniqueNumber);

        builder.Property(locker => locker.Version).IsRowVersion();
    }
}
