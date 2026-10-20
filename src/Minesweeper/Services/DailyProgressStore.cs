namespace Minesweeper.Services;

/// <summary>Which calendar dates the player has completed the Daily Challenge on -- Preferences-backed,
/// local only, same pattern as every other *Store in this app. Added because the Daily card had no memory of
/// past days at all (owner's report, 2026-10-20): "thử thách hôm nay" should really be "thử thách mỗi ngày",
/// with a calendar behind it showing which days are done. A date is recorded the moment GamePage's
/// Daily-mode HandleGameEndIfNeeded sees a win -- a day that was played and lost is deliberately NOT
/// recorded here, so DailyCalendarPage can only tell "completed" (green) apart from "not completed" (red);
/// it has no stored signal to further split "played but lost" from "never opened that day", and the owner's
/// spec does not ask for one.</summary>
public sealed class DailyProgressStore
{
    private const string CompletedKey = "daily_completed_dates";
    private const string FirstTrackedKey = "daily_first_tracked_date";

    public DailyProgressStore()
    {
        // Set once, the first time this store is ever touched on this install -- so DailyCalendarPage's
        // "red dot" rule (see FirstTrackedDate) never paints years of pre-install history as missed days.
        if (!Preferences.Default.ContainsKey(FirstTrackedKey))
            Preferences.Default.Set(FirstTrackedKey, DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"));
    }

    /// <summary>The earliest date DailyCalendarPage may mark as a miss; everything before it is shown with
    /// no mark at all, not a red dot, since the app was not tracking (or did not exist for this player) yet.</summary>
    public DateOnly FirstTrackedDate
    {
        get
        {
            string stored = Preferences.Default.Get(FirstTrackedKey, string.Empty);
            return DateOnly.TryParse(stored, out DateOnly date) ? date : DateOnly.FromDateTime(DateTime.UtcNow);
        }
    }

    public bool IsCompleted(DateOnly date) => LoadCompletedDates().Contains(date);

    public void MarkCompleted(DateOnly date)
    {
        HashSet<DateOnly> dates = LoadCompletedDates();
        if (!dates.Add(date)) return;
        Preferences.Default.Set(CompletedKey, string.Join(',', dates.Select(d => d.ToString("yyyy-MM-dd"))));
    }

    private static HashSet<DateOnly> LoadCompletedDates()
    {
        string raw = Preferences.Default.Get(CompletedKey, string.Empty);
        var result = new HashSet<DateOnly>();
        if (string.IsNullOrEmpty(raw)) return result;
        foreach (string part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
            if (DateOnly.TryParse(part, out DateOnly d)) result.Add(d);
        return result;
    }
}
