namespace Minesweeper.Views;

/// <summary>One cell in DailyCalendarPage's 7-column month grid. Day 0 is a blank filler cell (padding before
/// the 1st of the month lands on its correct weekday column) -- plain data, rebuilt fresh every time the page
/// loads a month, same "not observable" reasoning as LevelsPage's own LevelItem.</summary>
public sealed class CalendarDayItem
{
    public required int Day { get; init; }
    public required bool IsToday { get; init; }
    public required bool IsCompleted { get; init; }
    public required bool IsMissed { get; init; }

    public bool IsBlank => Day == 0;
    public string DayText => IsBlank ? string.Empty : Day.ToString();
}
