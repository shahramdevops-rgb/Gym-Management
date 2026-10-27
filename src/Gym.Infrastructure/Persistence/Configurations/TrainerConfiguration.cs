using Gym.Application.Trainers;
using Gym.Domain.Trainers;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>
/// The <c>trainers</c> table (BUSINESS_RULES.md §15). The rules live in <see cref="Trainer"/>; the
/// database repeats the ones it can check.
/// </summary>
public sealed class TrainerConfiguration : IEntityTypeConfiguration<Trainer>
{
    /// <summary>E.164 allows at most 15 digits after the plus sign.</summary>
    private const int PhoneMaxLength = 16;

    public void Configure(EntityTypeBuilder<Trainer> builder)
    {
        builder.ToTable("trainers", table =>
        {
            table.HasCheckConstraint("ck_trainers_full_name_not_blank", "btrim(full_name) <> ''");
            table.HasCheckConstraint("ck_trainers_phone_number_e164", "phone_number ~ '^\\+[0-9]{7,15}$'");
        });

        builder.Property(trainer => trainer.FullName).HasMaxLength(Trainer.FullNameMaxLength).IsRequired();
        builder.Property(trainer => trainer.PhoneNumber).HasMaxLength(PhoneMaxLength).IsRequired();

        // Stopped trainers included, so no filter: the phone stays theirs for when they come back.
        // Not unique against members: a trainer may be a member too, with the same phone.
        builder.HasIndex(trainer => trainer.PhoneNumber)
            .IsUnique()
            .HasDatabaseName(TrainerConstraints.UniquePhone);

        // Trainers are stopped, never deleted; the cascade only says a period means nothing alone.
        builder.HasMany(trainer => trainer.Periods)
            .WithOne()
            .HasForeignKey(period => period.TrainerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(trainer => trainer.Periods).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(trainer => trainer.Version).IsRowVersion();
    }
}
