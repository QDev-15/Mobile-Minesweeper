using Minesweeper.Core;

namespace Minesweeper.Services;

/// <summary>Best completion time per difficulty, local only -- no account needed, same "privacy-first"
/// stance as PdfReader (plan section 4, Tier 1). Uses <see cref="Preferences"/> directly, same as every
/// other small persisted flag in the sibling apps; nothing here needs a real database.</summary>
public sealed class GameStatsStore
{
    /// <summary>Seconds, or null if this difficulty has never been won.</summary>
    public int? BestTime(DifficultyLevel level) => Preferences.Default.ContainsKey(Key(level))
        ? Preferences.Default.Get(Key(level), 0)
        : null;

    /// <summary>Records a win's elapsed time; only ever lowers the stored best. Returns true if this was a
    /// new best (for a "New record!" moment on the result screen).</summary>
    public bool RecordWin(DifficultyLevel level, int elapsedSeconds)
    {
        int? current = BestTime(level);
        if (current is { } best && best <= elapsedSeconds) return false;
        Preferences.Default.Set(Key(level), elapsedSeconds);
        return true;
    }

    private static string Key(DifficultyLevel level) => $"best_time_{level}";
}
