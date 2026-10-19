using Minesweeper.Core;

namespace Minesweeper.Core.Tests;

public class DailySeedTests
{
    [Fact]
    public void SameDateAndDifficulty_SameSeed()
    {
        var date = new DateOnly(2026, 10, 17);
        Assert.Equal(DailySeed.For(date, DifficultyLevel.Medium), DailySeed.For(date, DifficultyLevel.Medium));
    }

    [Fact]
    public void DifferentDates_DifferentSeeds()
    {
        int a = DailySeed.For(new DateOnly(2026, 10, 17), DifficultyLevel.Medium);
        int b = DailySeed.For(new DateOnly(2026, 10, 18), DifficultyLevel.Medium);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void DifferentDifficulty_DifferentSeed_SameDate()
    {
        var date = new DateOnly(2026, 10, 17);
        int easy = DailySeed.For(date, DifficultyLevel.Easy);
        int hard = DailySeed.For(date, DifficultyLevel.Hard);
        Assert.NotEqual(easy, hard);
    }

    [Fact]
    public void DailyChallenge_ProducesABoard_ForToday()
    {
        Board? board = DailyChallenge.BoardFor(new DateOnly(2026, 10, 17));
        Assert.NotNull(board);
        Assert.Equal(DailyChallenge.Difficulty.MineCount, board!.MineCount());
    }

    [Fact]
    public void DailyChallenge_SameDate_ProducesIdenticalBoard()
    {
        // The whole point of Daily Challenge: every device must derive the same layout independently.
        var date = new DateOnly(2026, 10, 17);
        Board? a = DailyChallenge.BoardFor(date);
        Board? b = DailyChallenge.BoardFor(date);
        Assert.NotNull(a);
        Assert.NotNull(b);
        for (int x = 0; x < a!.Width; x++)
            for (int y = 0; y < a.Height; y++)
                Assert.Equal(a.IsMine(x, y), b!.IsMine(x, y));
    }
}
