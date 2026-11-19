namespace Minesweeper.Views;

/// <summary>One cell in DailyCalendarPage's 7-column month grid. Day 0 is a blank filler cell (padding before
/// the 1st of the month lands on its correct weekday column) -- plain data, rebuilt fresh every time the page
/// loads a month, same "not observable" reasoning as LevelsPage's own LevelItem.</summary>
public sealed class CalendarDayItem
{
    public required int Day { get; init; }
    public required DateOnly Date { get; init; }
    public required bool IsToday { get; init; }
    public required bool IsCompleted { get; init; }
    public required bool IsMissed { get; init; }
    // Only today or an earlier, already-tracked day actually has a playable Daily Challenge board behind it
    // -- a blank filler cell has no real date at all, and there is deliberately no board for a future date.
    public required bool IsSelectable { get; init; }
    // The day "Chơi ngay" currently targets (owner's direction, 2026-10-20: picking a day no longer jumps
    // straight into it -- tap selects it here first, Chơi ngay plays whichever day is selected).
    public required bool IsSelected { get; init; }

    // Null for a blank filler cell, or a real day that was never completed (or was completed before
    // 2026-11-18, when DailyProgressStore started recording per-day times at all) -- not `required` since
    // the blank filler cells built in DailyCalendarPage.LoadMonth never set it.
    public int? BestTimeSeconds { get; init; }

    public bool IsBlank => Day == 0;
    public string DayText => IsBlank ? string.Empty : Day.ToString();
    public bool HasBestTime => BestTimeSeconds is not null;
    public string BestTimeText => BestTimeSeconds is { } s ? $"{s / 60}:{s % 60:00}" : string.Empty;
}
