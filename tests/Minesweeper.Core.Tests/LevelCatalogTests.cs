using Minesweeper.Core;

namespace Minesweeper.Core.Tests;

public class LevelCatalogTests
{
    [Fact]
    public void Level1_IsSmallAndGentle()
    {
        Difficulty d = LevelCatalog.DifficultyFor(1);
        Assert.Equal(5, d.Width);
        Assert.Equal(5, d.Height);
        Assert.True(d.MineCount >= 1 && d.MineCount < 10);
    }

    [Fact]
    public void Level100_MatchesHardExactly()
    {
        // By construction (t=1.0 at level 100 interpolates straight to Hard's own width/height/density):
        // the hardest Tutorial level is exactly the already-measured Hard board, not a new, unverified size.
        Difficulty d = LevelCatalog.DifficultyFor(100);
        Assert.Equal(Difficulty.Hard.Width, d.Width);
        Assert.Equal(Difficulty.Hard.Height, d.Height);
        Assert.Equal(Difficulty.Hard.MineCount, d.MineCount);
    }

    [Fact]
    public void BoardSize_NeverShrinks_AsLevelIncreases()
    {
        Difficulty previous = LevelCatalog.DifficultyFor(1);
        for (int level = 2; level <= LevelCatalog.MaxLevel; level++)
        {
            Difficulty current = LevelCatalog.DifficultyFor(level);
            Assert.True(current.Width >= previous.Width, $"Width dropped at level {level}");
            Assert.True(current.Height >= previous.Height, $"Height dropped at level {level}");
            previous = current;
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    [InlineData(-5)]
    public void OutOfRangeLevel_Throws(int level) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => LevelCatalog.DifficultyFor(level));

    [Fact]
    public void AllLevels_LeaveEnoughSafeMarginForFirstClick()
    {
        for (int level = LevelCatalog.MinLevel; level <= LevelCatalog.MaxLevel; level++)
        {
            Difficulty d = LevelCatalog.DifficultyFor(level);
            int safeCells = d.Width * d.Height - d.MineCount;
            Assert.True(safeCells >= 10, $"Level {level} leaves only {safeCells} safe cells");
        }
    }
}
