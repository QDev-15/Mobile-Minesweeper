using Minesweeper.Core;

namespace Minesweeper.Core.Tests;

public class LevelStarsTests
{
    [Fact]
    public void VeryFastCompletion_Gets3Stars()
    {
        Assert.Equal(3, LevelStars.StarsFor(1, elapsedSeconds: 1));
    }

    [Fact]
    public void VerySlowCompletion_StillGetsAtLeast1Star()
    {
        Assert.Equal(1, LevelStars.StarsFor(1, elapsedSeconds: 100_000));
    }

    [Fact]
    public void Stars_NeverExceed3_OrGoBelow1()
    {
        for (int level = LevelCatalog.MinLevel; level <= LevelCatalog.MaxLevel; level += 7)
        {
            foreach (int elapsed in new[] { 0, 10, 60, 600, 36000 })
            {
                int stars = LevelStars.StarsFor(level, elapsed);
                Assert.InRange(stars, 1, 3);
            }
        }
    }

    [Fact]
    public void SlowerTime_NeverScoresMoreStarsThanFasterTime()
    {
        int fast = LevelStars.StarsFor(50, elapsedSeconds: 10);
        int slow = LevelStars.StarsFor(50, elapsedSeconds: 10_000);
        Assert.True(slow <= fast);
    }
}
