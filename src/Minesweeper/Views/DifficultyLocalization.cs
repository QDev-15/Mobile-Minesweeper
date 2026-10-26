using Minesweeper.Core;
using Minesweeper.Resources.Strings;

namespace Minesweeper.Views;

/// <summary>Difficulty.Name/DailyChallenge.Difficulty.Name/LevelCatalog's own per-level Name are all still
/// hardcoded Vietnamese (Minesweeper.Core has no reference to the UI/resx layer, and nothing actually reads
/// those specific .Name values for display any more -- confirmed by audit, 2026-10-26, before this file
/// existed). This is the one place a DifficultyLevel enum value becomes a localized, user-facing string
/// instead, for the couple of call sites (HomePage's resume card) that need one.</summary>
internal static class DifficultyLocalization
{
    public static string NameFor(DifficultyLevel level) => level switch
    {
        DifficultyLevel.Easy => AppStrings.Difficulty_Easy,
        DifficultyLevel.Medium => AppStrings.Difficulty_Medium,
        DifficultyLevel.Hard => AppStrings.Difficulty_Hard,
        _ => level.ToString(),
    };
}
