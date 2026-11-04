using Gym.Domain.Bookings;
using Gym.Domain.Members;
using Gym.Domain.Trainers;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>The <c>session_cancellations</c> table: the students' sessions a trainer's save cancelled (task 15.6c).</summary>
public sealed class SessionCancellationConfiguration : IEntityTypeConfiguration<SessionCancellation>
{
    public void Configure(EntityTypeBuilder<SessionCancellation> builder)
    {
        builder.ToTable("session_cancellations", table => TimetableSessionColumns.AddTimeChecks(table, "session_cancellations"));

        builder.HasOne<Trainer>().WithMany().HasForeignKey(row => row.TrainerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Member>().WithMany().HasForeignKey(row => row.MemberId).OnDelete(DeleteBehavior.Restrict);

        TimetableSessionColumns.Map(builder, row => row.Start, row => row.End);

        // One save cancels a student's session once; the SMS job reads a student's rows of one save.
        builder.HasIndex(row => new { row.ChangeId, row.MemberId, row.Date, row.Start, row.End }).IsUnique();

        // The member's home: their rows not seen yet.
        builder.HasIndex(row => new { row.MemberId, row.Date }).HasFilter("seen_at IS NULL");
    }
}
