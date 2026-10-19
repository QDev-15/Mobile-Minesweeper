namespace Minesweeper.Core;

/// <summary>Daily Challenge (plan section 3.3): always Medium difficulty, always No-guess, first click fixed
/// at the board's center -- so every player who opens the app on the same UTC date gets the exact same
/// solvable board, not just a board with the same mine count. Medium rather than Hard: No-guess generation
/// must reliably finish fast on a phone (section 3.2's named risk), and Medium is the largest size the
/// solver's tests confirm is comfortably fast.</summary>
public static class DailyChallenge
{
    public static readonly Difficulty Difficulty = Difficulty.Medium;

    /// <summary>Null only if No-guess generation could not find a solvable board within
    /// <paramref name="maxAttempts"/> for this date -- astronomically unlikely at 400 attempts on a 16x16
    /// board, but the caller must still handle it (e.g. fall back to yesterday's board, or a normal one)
    /// rather than crash.</summary>
    public static Board? BoardFor(DateOnly date, int maxAttempts = 400)
    {
        int seed = DailySeed.For(date, DifficultyLevel.Medium);
        var rng = new Random(seed);
        int centerX = Difficulty.Width / 2, centerY = Difficulty.Height / 2;
        return BoardGenerator.GenerateNoGuess(Difficulty, centerX, centerY, rng, maxAttempts);
    }
}
