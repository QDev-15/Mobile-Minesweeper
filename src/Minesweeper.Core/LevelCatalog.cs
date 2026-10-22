namespace Minesweeper.Core;

/// <summary>100 fixed levels (Tutorial mode), difficulty increasing smoothly from a tiny 5x5 board at level
/// 1 up to <see cref="Difficulty.Hard"/>'s own size (not its mine density -- see MaxDensity) at level 100 --
/// deliberately capped at Hard's dimensions rather than going beyond them, since that is the largest board
/// <c>NoGuessPerformanceTests</c> actually measures; going bigger would be sizing a board from
/// extrapolation, not data.</summary>
public static class LevelCatalog
{
    public const int MinLevel = 1;
    public const int MaxLevel = 100;

    // Tutorial is always No-guess (OnAppearing forces noGuess=true for every level), unlike Difficulty.Hard
    // itself which no longer is (owner's direction, 2026-10-21: 99 mines on a 288-cell board measured as a
    // reliable No-guess failure, 10/10 attempts even at the real 400-attempt budget -- Hard's own mine count
    // is deliberately NOT what level 100 targets any more). 60 mines/288 cells (20.8%) is the top of the
    // density range NoGuessPerformanceTests-style measurement actually confirmed reliable (0/10 failures,
    // ~65ms/attempt average); 65 still passed 0/10 but close enough to the cliff (70 already failed 2/10)
    // that this keeps a real margin rather than sitting right on the edge.
    private const double MaxDensity = 60.0 / 288.0;

    public static Difficulty DifficultyFor(int level)
    {
        if (level < MinLevel || level > MaxLevel)
            throw new ArgumentOutOfRangeException(nameof(level), level, $"Level must be {MinLevel}-{MaxLevel}.");

        double t = (level - 1) / (double)(MaxLevel - MinLevel); // 0.0 at level 1, 1.0 at level 100

        int width = (int)Math.Round(Lerp(5, Difficulty.Hard.Width, t));
        int height = (int)Math.Round(Lerp(5, Difficulty.Hard.Height, t));

        double easyDensity = Difficulty.Easy.MineCount / (double)(Difficulty.Easy.Width * Difficulty.Easy.Height);
        double density = Lerp(easyDensity * 0.8, MaxDensity, t); // level 1 starts gentler than Easy's own density

        int totalCells = width * height;
        // Leave at least a 10-cell safe margin beyond the first-click exclusion zone, so GenerateNoGuess
        // always has somewhere to place a solvable opening regardless of where the player's first tap lands.
        int mineCount = Math.Clamp((int)Math.Round(totalCells * density), 1, totalCells - 10);

        return new Difficulty($"Màn {level}", width, height, mineCount);
    }

    private static double Lerp(double from, double to, double t) => from + (to - from) * t;
}
