using Minesweeper.Core;

namespace Minesweeper.Services;

/// <summary>Tutorial mode progress: which levels are unlocked, and the best star rating (1-3) earned on
/// each. Local only (Preferences), same "no account" stance as the rest of the app -- per-level keys rather
/// than one JSON blob, since this grows and shrinks by single levels, not as one record.</summary>
public sealed class LevelProgressStore
{
    private const string UnlockedKey = "level_unlocked";
    private const string StarsKeyPrefix = "level_stars_";

    /// <summary>The highest level the player may currently play. Starts at 1 (level 1 is always unlocked).</summary>
    public int UnlockedLevel => Math.Clamp(Preferences.Default.Get(UnlockedKey, LevelCatalog.MinLevel), LevelCatalog.MinLevel, LevelCatalog.MaxLevel);

    public bool IsUnlocked(int level) => level <= UnlockedLevel;

    /// <summary>0 if never completed, otherwise the best (highest) star rating earned.</summary>
    public int StarsFor(int level) => Preferences.Default.Get(StarsKeyPrefix + level, 0);

    /// <summary>Call on winning a level: raises its best star rating if this run beat it, and unlocks the
    /// next level if this was the current frontier.</summary>
    public void RecordCompletion(int level, int stars)
    {
        if (stars > StarsFor(level)) Preferences.Default.Set(StarsKeyPrefix + level, stars);
        if (level == UnlockedLevel && level < LevelCatalog.MaxLevel) Preferences.Default.Set(UnlockedKey, level + 1);
    }
}
