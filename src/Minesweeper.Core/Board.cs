namespace Minesweeper.Core;

/// <summary>Immutable ground truth: mine layout + precomputed adjacency counts. Player-visible state
/// (revealed/flagged) is tracked separately by <see cref="GameEngine"/> and by <see cref="NoGuessSolver"/>'s
/// own simulation, never here -- keeps "what the solver is allowed to look at" cleanly separate from "what is
/// actually true", so a deduction can never accidentally peek at the mine layout.</summary>
public sealed class Board
{
    public int Width { get; }
    public int Height { get; }
    private readonly bool[,] _mines;
    private readonly int[,] _adjacent;

    public Board(int width, int height, bool[,] mines)
    {
        Width = width;
        Height = height;
        _mines = mines;
        _adjacent = new int[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                _adjacent[x, y] = mines[x, y] ? -1 : CountAdjacentMines(x, y);
    }

    public bool IsMine(int x, int y) => _mines[x, y];

    /// <summary>-1 at a mine cell; otherwise 0-8, the number a real reveal shows.</summary>
    public int AdjacentMines(int x, int y) => _adjacent[x, y];

    public bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

    public IEnumerable<(int X, int Y)> Neighbors(int x, int y)
    {
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx, ny = y + dy;
                if (InBounds(nx, ny)) yield return (nx, ny);
            }
    }

    public int MineCount()
    {
        int count = 0;
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                if (_mines[x, y]) count++;
        return count;
    }

    private int CountAdjacentMines(int x, int y)
    {
        int count = 0;
        foreach ((int nx, int ny) in Neighbors(x, y))
            if (_mines[nx, ny]) count++;
        return count;
    }
}
