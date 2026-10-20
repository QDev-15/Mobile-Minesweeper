namespace Minesweeper.Views;

/// <summary>One tile in <see cref="LevelsPage"/>'s grid. Plain data, rebuilt fresh every time the page
/// appears (LevelProgressStore is the source of truth) -- not observable, there is nothing to mutate in place.</summary>
public sealed class LevelItem
{
    public required int Number { get; init; }
    public required bool Unlocked { get; init; }
    public required int Stars { get; init; }

    /// <summary>True for exactly one tile: the highest unlocked level (the player's actual "play next"
    /// frontier) -- highlighted in LevelsPage.xaml so "what do I play next" is answered at a glance in a
    /// 100-tile grid, not something the player has to hunt for.</summary>
    public required bool IsNext { get; init; }

    public string NumberText => Unlocked ? Number.ToString() : Icons.Lock;

    // Always exactly 3 stars, win or lock: a locked tile's Stars is always 0, so these come out all-false on
    // their own -- no separate "locked -> 3 gray stars" branch needed. LevelsPage.xaml's DataTrigger per star
    // swaps StarBorder/gray for Star/Accent-gold only when that star is actually earned.
    public bool Star1 => Stars >= 1;
    public bool Star2 => Stars >= 2;
    public bool Star3 => Stars >= 3;

    /// <summary>A locked tile needs to read as "not available yet" at a glance, not just differ by its icon
    /// -- dimming the whole card (background, border and icon together via Border.Opacity) is a cheaper and
    /// clearer signal than a converter-driven color swap would be.</summary>
    public double CardOpacity => Unlocked ? 1.0 : 0.4;
}
