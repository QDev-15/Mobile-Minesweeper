namespace Minesweeper.Core;

public static class BoardGenerator
{
    /// <summary>Places mines so (firstX, firstY) and its immediate neighbors are never mines -- standard
    /// "first click is always safe, and always opens onto something" rule, same as real Minesweeper.</summary>
    public static Board Generate(Difficulty difficulty, int firstX, int firstY, Random rng)
    {
        var safe = new HashSet<(int, int)> { (firstX, firstY) };
        foreach (var n in NeighborsOf(firstX, firstY, difficulty.Width, difficulty.Height)) safe.Add(n);

        var cells = new List<(int, int)>(difficulty.Width * difficulty.Height);
        for (int x = 0; x < difficulty.Width; x++)
            for (int y = 0; y < difficulty.Height; y++)
                if (!safe.Contains((x, y))) cells.Add((x, y));

        // Fisher-Yates: shuffle the non-safe pool, then take the first MineCount as mine locations.
        for (int i = cells.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (cells[i], cells[j]) = (cells[j], cells[i]);
        }

        var mines = new bool[difficulty.Width, difficulty.Height];
        int toPlace = Math.Min(difficulty.MineCount, cells.Count);
        for (int i = 0; i < toPlace; i++)
        {
            var (x, y) = cells[i];
            mines[x, y] = true;
        }

        return new Board(difficulty.Width, difficulty.Height, mines);
    }

    /// <summary>Generate-and-check loop for No-guess mode (plan section 3.2): keeps generating fresh boards
    /// for the same first click until <see cref="NoGuessSolver"/> confirms one is fully solvable by logic
    /// alone, or gives up after <paramref name="maxAttempts"/>. Null on give-up -- the caller decides the
    /// fallback (e.g. fall back to a normal board, or tell the player and let them retry); never returns a
    /// board silently without checking it, that would break the one promise No-guess mode makes.</summary>
    public static Board? GenerateNoGuess(Difficulty difficulty, int firstX, int firstY, Random rng, int maxAttempts = 400)
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Board board = Generate(difficulty, firstX, firstY, rng);
            if (NoGuessSolver.IsFullySolvable(board, firstX, firstY)) return board;
        }
        return null;
    }

    private static IEnumerable<(int, int)> NeighborsOf(int x, int y, int width, int height)
    {
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && nx < width && ny >= 0 && ny < height) yield return (nx, ny);
            }
    }
}
