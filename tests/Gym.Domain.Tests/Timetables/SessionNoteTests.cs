using Gym.Domain.Timetables;

namespace Gym.Domain.Tests.Timetables;

public sealed class SessionNoteTests
{
    private static readonly Guid TrainerId = Guid.CreateVersion7();

    /// <summary>The gym's today, named rather than read from a clock.</summary>
    private static readonly DateOnly Today = new(2026, 11, 11);

    private static readonly TimeOnly Noon = new(12, 0);

    private static readonly TimeOnly Start = new(13, 0);

    private static readonly TimeOnly End = new(14, 0);

    [Fact]
    public void Write_TomorrowsSession_KeepsTheTextTrimmed()
    {
        var note = SessionNote.Write(TrainerId, Today.AddDays(1), Start, End, "  فردا تمرین پا است  ", Today, Noon).Value;

        note.Text.ShouldBe("فردا تمرین پا است");
        note.Date.ShouldBe(Today.AddDays(1));
        note.Start.ShouldBe(Start);
        note.End.ShouldBe(End);
    }

    [Fact]
    public void Write_TodaysSessionStillRunning_Succeeds()
    {
        SessionNote.Write(TrainerId, Today, new TimeOnly(11, 0), End, "دیرتر شروع می‌کنیم", Today, Noon).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Write_TheTimetablesLastDay_Succeeds()
    {
        SessionNote.Write(TrainerId, TimetableRules.LastDay(Today), Start, End, "یادداشت", Today, Noon).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Write_PastTheTimetablesLastDay_FailsWithDayOutOfRange()
    {
        SessionNote.Write(TrainerId, TimetableRules.LastDay(Today).AddDays(1), Start, End, "یادداشت", Today, Noon)
            .Error.ShouldBe(TimetableErrors.DayOutOfRange);
    }

    [Fact]
    public void Write_TodaysSessionThatHasEnded_FailsWithNoteSessionEnded()
    {
        SessionNote.Write(TrainerId, Today, new TimeOnly(10, 0), new TimeOnly(11, 0), "یادداشت", Today, Noon)
            .Error.ShouldBe(TimetableErrors.NoteSessionEnded);
    }

    [Fact]
    public void Write_TodaysSessionEndingNow_FailsWithNoteSessionEnded()
    {
        SessionNote.Write(TrainerId, Today, new TimeOnly(11, 0), Noon, "یادداشت", Today, Noon)
            .Error.ShouldBe(TimetableErrors.NoteSessionEnded);
    }

    [Fact]
    public void Write_Yesterday_FailsWithNoteSessionEnded()
    {
        SessionNote.Write(TrainerId, Today.AddDays(-1), Start, End, "یادداشت", Today, Noon)
            .Error.ShouldBe(TimetableErrors.NoteSessionEnded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \n ")]
    public void Write_BlankText_FailsWithNoteEmpty(string? text)
    {
        SessionNote.Write(TrainerId, Today.AddDays(1), Start, End, text, Today, Noon).Error.ShouldBe(TimetableErrors.NoteEmpty);
    }

    [Fact]
    public void Write_TextAtTheLimit_Succeeds()
    {
        SessionNote.Write(TrainerId, Today.AddDays(1), Start, End, new string('ن', SessionNoteText.MaxDateLength), Today, Noon)
            .IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Write_TextOverTheLimit_FailsWithDateNoteTooLong()
    {
        SessionNote.Write(TrainerId, Today.AddDays(1), Start, End, new string('ن', SessionNoteText.MaxDateLength + 1), Today, Noon)
            .Error.ShouldBe(TimetableErrors.DateNoteTooLong);
    }

    [Fact]
    public void Write_EndNotAfterStart_Throws()
    {
        Should.Throw<ArgumentException>(() => SessionNote.Write(TrainerId, Today.AddDays(1), End, Start, "یادداشت", Today, Noon));
    }

    [Fact]
    public void Write_EmptyTrainer_Throws()
    {
        Should.Throw<ArgumentException>(() => SessionNote.Write(Guid.Empty, Today.AddDays(1), Start, End, "یادداشت", Today, Noon));
    }

    [Fact]
    public void Rewrite_BeforeTheSessionEnds_ChangesTheText()
    {
        var note = SessionNote.Write(TrainerId, Today, Start, End, "اول", Today, Noon).Value;

        note.Rewrite("دوم", Today, new TimeOnly(13, 30)).IsSuccess.ShouldBeTrue();

        note.Text.ShouldBe("دوم");
    }

    [Fact]
    public void Rewrite_AfterTheSessionEnded_FailsAndKeepsTheText()
    {
        var note = SessionNote.Write(TrainerId, Today, Start, End, "اول", Today, Noon).Value;

        note.Rewrite("دوم", Today, End).Error.ShouldBe(TimetableErrors.NoteSessionEnded);

        note.Text.ShouldBe("اول");
    }

    [Fact]
    public void Rewrite_BlankText_FailsAndKeepsTheText()
    {
        var note = SessionNote.Write(TrainerId, Today.AddDays(1), Start, End, "اول", Today, Noon).Value;

        note.Rewrite(" ", Today, Noon).Error.ShouldBe(TimetableErrors.NoteEmpty);

        note.Text.ShouldBe("اول");
    }

    [Fact]
    public void CheckChange_TheNextDay_FailsWithNoteSessionEnded()
    {
        var note = SessionNote.Write(TrainerId, Today, Start, End, "اول", Today, Noon).Value;

        note.CheckChange(Today.AddDays(1), new TimeOnly(8, 0)).Error.ShouldBe(TimetableErrors.NoteSessionEnded);
    }
}
