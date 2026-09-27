using Gym.Application.Lockers;
using Gym.Domain.Lockers;
using Gym.Infrastructure.Persistence.Seed;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>
/// The <c>lockers</c> table and its 72 seeded rows. The rules live in <see cref="Locker"/>; the
/// database repeats every one it can check, so a bug or a hand-written SQL statement cannot store
/// a locker the gym does not have.
/// </summary>
public sealed class LockerConfiguration : IEntityTypeConfiguration<Locker>
{
    public void Configure(EntityTypeBuilder<Locker> builder)
    {
        builder.ToTable("lockers", table =>
            table.HasCheckConstraint(LockerConstraints.NumberRange, $"number BETWEEN 1 AND {Locker.Count}"));

        builder.HasIndex(locker => locker.Number)
            .IsUnique()
            .HasDatabaseName(LockerConstraints.UniqueNumber);

        builder.Property(locker => locker.Version).IsRowVersion();

        // Anonymous objects, because every setter on the entity is private (the same as the
        // expense categories). Every locker starts in service.
        builder.HasData(LockerSeed.All.Select(seed => new
        {
            seed.Id,
            seed.Number,
            IsOutOfService = false,
            LockerSeed.CreatedAt,
        }));
    }
}
