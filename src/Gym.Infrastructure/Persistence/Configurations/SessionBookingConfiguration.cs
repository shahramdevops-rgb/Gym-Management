using Gym.Domain.Bookings;
using Gym.Domain.Coaching;
using Gym.Domain.Members;
using Gym.Domain.Trainers;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>The <c>session_bookings</c> table: the single dates a student moved a session to.</summary>
public sealed class SessionBookingConfiguration : IEntityTypeConfiguration<SessionBooking>
{
    public void Configure(EntityTypeBuilder<SessionBooking> builder)
    {
        builder.ToTable("session_bookings", table =>
        {
            TimetableSessionColumns.AddTimeChecks(table, "session_bookings");
            table.HasCheckConstraint("ck_session_bookings_booked_by", "booked_by IN ('Trainer', 'Member')");
        });

        builder.HasOne<Trainer>().WithMany().HasForeignKey(booking => booking.TrainerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Member>().WithMany().HasForeignKey(booking => booking.MemberId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TrainerLink>().WithMany().HasForeignKey(booking => booking.LinkId).OnDelete(DeleteBehavior.Restrict);

        TimetableSessionColumns.Map(builder, booking => booking.Start, booking => booking.End);
        builder.Property(booking => booking.By).HasColumnName("booked_by").HasConversion<string>().HasMaxLength(10);

        // A member is in one date's session once.
        builder.HasIndex(booking => new { booking.MemberId, booking.Date, booking.Start, booking.End }).IsUnique();

        // A trainer's places over a range of dates.
        builder.HasIndex(booking => new { booking.TrainerId, booking.Date });
    }
}
