using System.Diagnostics;
using Minesweeper.Core;

namespace Minesweeper.Core.Tests;

/// <summary>Directly answers the plan's named risk (section 3.2): is No-guess board generation fast enough
/// to run on a phone? These assert generous PC-side ceilings (a phone CPU is slower, but not by an order of
/// magnitude for this workload) and print the real observed time for each difficulty so a run's test output
/// is itself the evidence, not just a pass/fail.</summary>
public class NoGuessPerformanceTests
{
    [Theory]
    [InlineData(DifficultyLevel.Easy, 2000)]
    [InlineData(DifficultyLevel.Medium, 5000)]
    // Hard joined this Theory 2026-11-19, alongside Easy/Medium -- it is no longer the one difficulty that
    // skips No-guess (see Difficulty.cs/GamePage.xaml.cs's own comments on why), and its mine count was
    // lowered specifically so it belongs here instead of in its own "measured not asserted" test.
    [InlineData(DifficultyLevel.Hard, 5000)]
    public void GenerateNoGuess_FinishesWithinBudget(DifficultyLevel level, int maxMilliseconds)
    {
        Difficulty difficulty = Difficulty.For(level);
        var rng = new Random(123);
        int cx = difficulty.Width / 2, cy = difficulty.Height / 2;

        var sw = Stopwatch.StartNew();
        Board? board = BoardGenerator.GenerateNoGuess(difficulty, cx, cy, rng);
        sw.Stop();

        Console.WriteLine($"{level}: {sw.ElapsedMilliseconds} ms");
        Assert.NotNull(board);
        Assert.True(sw.ElapsedMilliseconds < maxMilliseconds,
            $"{level} No-guess generation took {sw.ElapsedMilliseconds} ms, over the {maxMilliseconds} ms budget.");
    }

    /// <summary>Tutorial IS always No-guess (GamePage forces noGuess=true for every level), so unlike Hard
    /// above, level 100 -- the actual hardest No-guess board the app ever generates -- must reliably
    /// succeed. LevelCatalog.MaxDensity (60 mines/288 cells) was chosen specifically to keep this true; this
    /// is the regression guard for that choice.</summary>
    [Fact]
    public void GenerateNoGuess_TutorialLevel100_Succeeds()
    {
        Difficulty difficulty = LevelCatalog.DifficultyFor(100);
        var rng = new Random(123);
        int cx = difficulty.Width / 2, cy = difficulty.Height / 2;

        var sw = Stopwatch.StartNew();
        Board? board = BoardGenerator.GenerateNoGuess(difficulty, cx, cy, rng);
        sw.Stop();

        Console.WriteLine($"Tutorial level 100 ({difficulty.Width}x{difficulty.Height}/{difficulty.MineCount}): {sw.ElapsedMilliseconds} ms, solved: {board != null}");
        Assert.NotNull(board);
    }

    /// <summary>DailyChallenge's own board (12x24/50 mines, 2026-10-20) is bigger than Hard (12x16/40) --
    /// same "measure, don't guess" reasoning as the Hard test above, since nothing else in this suite covers
    /// the actual largest board the app generates.</summary>
    [Fact]
    public void GenerateNoGuess_Daily_MeasuredNotAsserted()
    {
        var rng = new Random(123);
        Difficulty difficulty = DailyChallenge.Difficulty;
        int cx = difficulty.Width / 2, cy = difficulty.Height / 2;

        var sw = Stopwatch.StartNew();
        Board? board = BoardGenerator.GenerateNoGuess(difficulty, cx, cy, rng, maxAttempts: 100);
        sw.Stop();

        Console.WriteLine($"Daily ({difficulty.Width}x{difficulty.Height}/{difficulty.MineCount}): {sw.ElapsedMilliseconds} ms, solved: {board != null}");
    }
}
