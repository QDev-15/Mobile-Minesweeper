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

    /// <summary>Hard (30x16/99 mines) is the one the plan explicitly flagged as the likely-too-slow case.
    /// Not asserted against a budget -- this test exists to PRINT the real number so that decision (ship
    /// No-guess for Hard, or restrict it to Easy/Medium per the plan's documented fallback) is made from
    /// data, not a guess.</summary>
    [Fact]
    public void GenerateNoGuess_Hard_MeasuredNotAsserted()
    {
        var rng = new Random(123);
        int cx = Difficulty.Hard.Width / 2, cy = Difficulty.Hard.Height / 2;

        var sw = Stopwatch.StartNew();
        Board? board = BoardGenerator.GenerateNoGuess(Difficulty.Hard, cx, cy, rng, maxAttempts: 100);
        sw.Stop();

        Console.WriteLine($"Hard: {sw.ElapsedMilliseconds} ms, solved: {board != null}");
    }
}
