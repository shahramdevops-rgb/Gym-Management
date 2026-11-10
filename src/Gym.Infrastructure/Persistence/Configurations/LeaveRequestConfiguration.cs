using Gym.Domain.Bookings;
using Gym.Domain.Coaching;
using Gym.Domain.Members;
using Gym.Domain.Trainers;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>
/// The <c>leave_requests</c> table: a student asking their trainer for leave from one date of a
/// session, and the answer (BUSINESS_RULES.md §15 <i>Leave by request</i>, task 15.7i).
/// </summary>
public sealed class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    /// <summary>One waiting or accepted request per member, date and session times.</summary>
    public const string OneLivePerPlace = "ux_leave_requests_one_live_per_place";

    public void Configure(EntityTypeBuilder<LeaveRequest> builder)
    {
        builder.ToTable("leave_requests", table =>
        {
            TimetableSessionColumns.AddTimeChecks(table, "leave_requests");
            table.HasCheckConstraint("ck_leave_requests_status", "status IN ('Pending', 'Accepted', 'Declined', 'Cancelled')");

            // Asked by the day before (LeaveRequest.Create).
            table.HasCheckConstraint("ck_leave_requests_asked_before_the_date", "requested_on < date");
        });

        builder.HasOne<Trainer>().WithMany().HasForeignKey(request => request.TrainerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Member>().WithMany().HasForeignKey(request => request.MemberId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TrainerLink>().WithMany().HasForeignKey(request => request.LinkId).OnDelete(DeleteBehavior.Restrict);

        TimetableSessionColumns.Map(builder, request => request.Start, request => request.End);
        builder.Property(request => request.Status).HasConversion<string>().HasMaxLength(10);

        builder.HasIndex(request => new { request.MemberId, request.Date, request.Start, request.End }, "OneLivePerPlace")
            .IsUnique()
            .HasFilter("status IN ('Pending', 'Accepted')")
            .HasDatabaseName(OneLivePerPlace);

        // The trainer's requests waiting, and their accepted leaves over a range of dates.
        builder.HasIndex(request => new { request.TrainerId, request.Status, request.Date });

        builder.Property(request => request.Version).IsRowVersion();
    }
}
