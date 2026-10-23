namespace Minesweeper.Core;

public static class BoardGenerator
{
    /// <summary>Places mines so (firstX, firstY) and its immediate neighbors are never mines -- standard
    /// "first click is always safe, and always opens onto something" rule, same as real Minesweeper. Still
    /// used by No-guess generation (GenerateNoGuess below), which needs a real anchor cell to build its
    /// solvability proof around; GeneratePureRandom is the one GamePage actually calls when No-guess is
    /// off (owner's direction, 2026-10-21: no cell should be specially protected any more).</summary>
    public static Board Generate(Difficulty difficulty, int firstX, int firstY, Random rng)
    {
        var safe = new HashSet<(int, int)> { (firstX, firstY) };
        foreach (var n in NeighborsOf(firstX, firstY, difficulty.Width, difficulty.Height)) safe.Add(n);
        return PlaceMines(difficulty, rng, safe);
    }

    /// <summary>No excluded cell at all -- every cell, including wherever the player's first tap lands, has
    /// an equal chance of being a mine (owner's direction, 2026-10-21: the board/mine layout must already
    /// exist the moment a level opens, not deferred to and protected around the player's first tap).</summary>
    public static Board GeneratePureRandom(Difficulty difficulty, Random rng) => PlaceMines(difficulty, rng, []);

    private static Board PlaceMines(Difficulty difficulty, Random rng, HashSet<(int, int)> excluded)
    {
        var cells = new List<(int, int)>(difficulty.Width * difficulty.Height);
        for (int x = 0; x < difficulty.Width; x++)
            for (int y = 0; y < difficulty.Height; y++)
                if (!excluded.Contains((x, y))) cells.Add((x, y));

        // Fisher-Yates: shuffle the eligible pool, then take the first MineCount as mine locations.
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
    /// alone, or gives up after roughly <paramref name="maxAttempts"/> tries in total. Null on give-up -- the
    /// caller decides the fallback (e.g. fall back to a normal board, or tell the player and let them retry);
    /// never returns a board silently without checking it, that would break the one promise No-guess mode
    /// makes.
    ///
    /// Spreads those attempts across every available CPU core instead of trying them one at a time on a
    /// single thread (owner's direction, 2026-10-22: board generation was the dominant cost in "click play ->
    /// board ready", and every attempt here is fully independent -- a fresh random board, an isolated solve
    /// check -- so there is nothing to coordinate between them beyond stopping the others once one succeeds).
    /// A phone with N cores finishes in roughly 1/N the wall-clock time for the same total attempt budget, or
    /// equivalently tries roughly N times as many boards in the same wall-clock time -- which also narrows the
    /// gap for denser difficulties, without changing the solver itself or its accept/reject guarantee at all.
    /// Each worker gets its OWN Random, seeded from the caller's <paramref name="rng"/> sequentially on THIS
    /// thread before any parallelism starts: System.Random (unlike Random.Shared) is not thread-safe, and the
    /// caller is not guaranteed to have passed Random.Shared (the test suite passes a seeded Random for
    /// reproducibility) -- sharing one Random instance across the parallel workers below would corrupt it.
    /// Falls back to the plain sequential loop when only one core is actually available, so that path still
    /// exists and stays simple rather than running Parallel.For with a single worker for no reason.</summary>
    public static Board? GenerateNoGuess(Difficulty difficulty, int firstX, int firstY, Random rng, int maxAttempts = 400)
    {
        int workerCount = Math.Clamp(Environment.ProcessorCount, 1, 8);
        if (workerCount == 1)
        {
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                Board board = Generate(difficulty, firstX, firstY, rng);
                if (NoGuessSolver.IsFullySolvable(board, firstX, firstY)) return board;
            }
            return null;
        }

        var workerRngs = new Random[workerCount];
        for (int i = 0; i < workerCount; i++) workerRngs[i] = new Random(rng.Next());

        int attemptsPerWorker = (maxAttempts + workerCount - 1) / workerCount; // ceil: total tries across all workers is >= maxAttempts, never less
        Board? found = null;
        object sync = new();

        Parallel.For(0, workerCount, (workerIndex, state) =>
        {
            Random workerRng = workerRngs[workerIndex];
            for (int attempt = 0; attempt < attemptsPerWorker; attempt++)
            {
                if (state.IsStopped) return; // another worker already found one -- stop starting new attempts here too
                Board board = Generate(difficulty, firstX, firstY, workerRng);
                if (!NoGuessSolver.IsFullySolvable(board, firstX, firstY)) continue;
                lock (sync) { found ??= board; } // first worker to get here wins; a near-simultaneous second success is just discarded
                state.Stop();
                return;
            }
        });

        return found;
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
