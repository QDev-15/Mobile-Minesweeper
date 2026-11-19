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
    private const string BestTimesKey = "daily_best_times";
    private const string FirstTrackedKey = "daily_first_tracked_date";

    public DailyProgressStore()
    {
        // Set once, the first time this store is ever touched on this install -- so DailyCalendarPage's
        // "red dot" rule (see FirstTrackedDate) never paints years of pre-install history as missed days.
        // Local date, not UTC (see DateOnly.FromDateTime(DateTime.Now) below) -- the day boundary players
        // actually experience is their own midnight, not UTC midnight.
        if (!Preferences.Default.ContainsKey(FirstTrackedKey))
            Preferences.Default.Set(FirstTrackedKey, DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd"));
    }

    /// <summary>The earliest date DailyCalendarPage may mark as a miss; everything before it is shown with
    /// no mark at all, not a red dot, since the app was not tracking (or did not exist for this player) yet.</summary>
    public DateOnly FirstTrackedDate
    {
        get
        {
            string stored = Preferences.Default.Get(FirstTrackedKey, string.Empty);
            return DateOnly.TryParse(stored, out DateOnly date) ? date : DateOnly.FromDateTime(DateTime.Now);
        }
    }

    public bool IsCompleted(DateOnly date) => LoadCompletedDates().Contains(date);

    public void MarkCompleted(DateOnly date)
    {
        HashSet<DateOnly> dates = LoadCompletedDates();
        if (!dates.Add(date)) return;
        Preferences.Default.Set(CompletedKey, string.Join(',', dates.Select(d => d.ToString("yyyy-MM-dd"))));
    }

    /// <summary>Best (shortest) winning time recorded for that specific calendar date, or null if that date
    /// has never been won.</summary>
    public int? BestTime(DateOnly date) => LoadBestTimes().TryGetValue(date, out int seconds) ? seconds : null;

    /// <summary>Records a Daily Challenge win for this date: marks the date completed, and keeps the stored
    /// time only if it beats that date's own previous best (a tie does not count, same rule as
    /// GameStatsStore.RecordWin). Returns true when this call set a new record for the date.</summary>
    public bool RecordWin(DateOnly date, int elapsedSeconds)
    {
        MarkCompleted(date);

        Dictionary<DateOnly, int> times = LoadBestTimes();
        if (times.TryGetValue(date, out int current) && current <= elapsedSeconds) return false;

        times[date] = elapsedSeconds;
        Preferences.Default.Set(BestTimesKey, string.Join(',', times.Select(kv => $"{kv.Key:yyyy-MM-dd}:{kv.Value}")));
        return true;
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

    private static Dictionary<DateOnly, int> LoadBestTimes()
    {
        string raw = Preferences.Default.Get(BestTimesKey, string.Empty);
        var result = new Dictionary<DateOnly, int>();
        if (string.IsNullOrEmpty(raw)) return result;
        foreach (string part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] pieces = part.Split(':');
            if (pieces.Length == 2 && DateOnly.TryParse(pieces[0], out DateOnly d) && int.TryParse(pieces[1], out int s))
                result[d] = s;
        }
        return result;
    }
}
