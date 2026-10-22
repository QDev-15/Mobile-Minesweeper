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

    public bool IsBlank => Day == 0;
    public string DayText => IsBlank ? string.Empty : Day.ToString();
}
