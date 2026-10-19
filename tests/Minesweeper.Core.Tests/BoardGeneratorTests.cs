using Minesweeper.Core;

namespace Minesweeper.Core.Tests;

public class BoardGeneratorTests
{
    [Fact]
    public void FirstClickAndNeighbors_AreNeverMines()
    {
        var rng = new Random(42);
        for (int trial = 0; trial < 50; trial++)
        {
            Board board = BoardGenerator.Generate(Difficulty.Medium, 8, 8, rng);
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    Assert.False(board.IsMine(8 + dx, 8 + dy));
        }
    }

    [Fact]
    public void Generate_PlacesExactlyMineCountMines()
    {
        var rng = new Random(7);
        Board board = BoardGenerator.Generate(Difficulty.Hard, 0, 0, rng);
        Assert.Equal(Difficulty.Hard.MineCount, board.MineCount());
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(8, 8)]
    [InlineData(15, 15)]
    public void Generate_WorksFromAnyCorner(int x, int y)
    {
        var rng = new Random(1);
        Board board = BoardGenerator.Generate(Difficulty.Medium, x, y, rng);
        Assert.Equal(Difficulty.Medium.MineCount, board.MineCount());
        Assert.False(board.IsMine(x, y));
    }
}
