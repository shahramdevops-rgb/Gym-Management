using Gym.Domain.Timetables;
using Gym.Domain.Trainers;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>The <c>session_notes</c> table: the trainer's note on one date's session (BUSINESS_RULES.md §15 <i>Session notes</i>, 15.7e).</summary>
public sealed class SessionNoteConfiguration : IEntityTypeConfiguration<SessionNote>
{
    public void Configure(EntityTypeBuilder<SessionNote> builder)
    {
        builder.ToTable("session_notes", table =>
        {
            TimetableSessionColumns.AddTimeChecks(table, "session_notes");
            table.HasCheckConstraint("ck_session_notes_text_not_blank", TimetableSessionColumns.NotBlank("text"));
        });

        builder.HasOne<Trainer>().WithMany().HasForeignKey(note => note.TrainerId).OnDelete(DeleteBehavior.Restrict);

        TimetableSessionColumns.Map(builder, note => note.Start, note => note.End);
        builder.Property(note => note.Text).HasMaxLength(SessionNoteText.MaxDateLength);

        // One note per session of a date; it also serves a trainer's notes over a range of dates.
        builder.HasIndex(note => new { note.TrainerId, note.Date, note.Start, note.End }).IsUnique();

        builder.Property(note => note.Version).IsRowVersion();
    }
}
