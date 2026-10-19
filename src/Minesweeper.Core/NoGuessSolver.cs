namespace Minesweeper.Core;

/// <summary>The one piece of logic No-guess mode and the in-game Hint feature both stand on (plan sections
/// 3.2 and 4/Tier 2). <see cref="DeduceOnePass"/> never looks at the real mine layout -- only at revealed
/// numbers and flags, exactly what a human player sees -- so every deduction it returns is a legitimate
/// logical proof, not a peek. <see cref="IsFullySolvable"/> reuses that same pass to simulate solving an
/// entire board at generation time (the only place ground truth is consulted, and only to answer "what
/// number does revealing this cell show", the same truthful answer a real reveal gives).</summary>
public static class NoGuessSolver
{
    public readonly record struct Deduction(IReadOnlyList<(int X, int Y)> Safe, IReadOnlyList<(int X, int Y)> Mines);

    /// <summary>One pass of pure logical deduction over what is currently visible: the basic rule (a
    /// number's remaining mine count, after subtracting its flagged neighbors, tells you whether its hidden
    /// neighbors are all safe or all mines) plus subset elimination between two overlapping constraints
    /// (A ⊆ B ⟹ B\A has exactly Remaining(B) - Remaining(A) mines). Returns everything this single pass can
    /// prove; callers loop it (each newly revealed/flagged cell can unlock further passes).</summary>
    public static Deduction DeduceOnePass(CellState[,] state, Func<int, int, int> numberAt, int width, int height)
    {
        var safe = new HashSet<(int, int)>();
        var mines = new HashSet<(int, int)>();

        var constraints = new List<(HashSet<(int, int)> Cells, int Remaining)>();
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (state[x, y] != CellState.Revealed) continue;
                int number = numberAt(x, y);
                if (number <= 0) continue;

                var hidden = new HashSet<(int, int)>();
                int flagged = 0;
                foreach (var (nx, ny) in NeighborsOf(x, y, width, height))
                {
                    if (state[nx, ny] == CellState.Hidden) hidden.Add((nx, ny));
                    else if (state[nx, ny] == CellState.Flagged) flagged++;
                }
                if (hidden.Count == 0) continue;
                constraints.Add((hidden, number - flagged));
            }

        foreach (var (cells, remaining) in constraints)
        {
            if (remaining <= 0) foreach (var c in cells) safe.Add(c);
            else if (remaining == cells.Count) foreach (var c in cells) mines.Add(c);
        }

        if (safe.Count == 0 && mines.Count == 0)
        {
            foreach (var a in constraints)
                foreach (var b in constraints)
                {
                    if (a.Cells.Count >= b.Cells.Count || !a.Cells.IsSubsetOf(b.Cells)) continue;
                    var diff = new HashSet<(int, int)>(b.Cells);
                    diff.ExceptWith(a.Cells);
                    int diffRemaining = b.Remaining - a.Remaining;
                    if (diffRemaining <= 0) foreach (var c in diff) safe.Add(c);
                    else if (diffRemaining == diff.Count) foreach (var c in diff) mines.Add(c);
                }
        }

        return new Deduction([.. safe], [.. mines]);
    }

    /// <summary>True if, starting from (firstX, firstY), the whole board can be cleared using only
    /// <see cref="DeduceOnePass"/> -- never forced to guess between equally-likely cells. Used by
    /// <see cref="BoardGenerator.GenerateNoGuess"/> to accept or reject a freshly generated board.</summary>
    public static bool IsFullySolvable(Board board, int firstX, int firstY)
    {
        var state = new CellState[board.Width, board.Height];
        int totalSafeCells = board.Width * board.Height - board.MineCount();
        int revealedCount = RevealFlood(board, state, firstX, firstY);

        while (revealedCount < totalSafeCells)
        {
            Deduction d = DeduceOnePass(state, board.AdjacentMines, board.Width, board.Height);
            if (d.Safe.Count == 0 && d.Mines.Count == 0) return false; // stuck: this board would force a guess here

            foreach (var (x, y) in d.Mines)
                if (state[x, y] == CellState.Hidden) state[x, y] = CellState.Flagged;

            foreach (var (x, y) in d.Safe)
                if (state[x, y] == CellState.Hidden)
                    revealedCount += RevealFlood(board, state, x, y);
        }
        return true;
    }

    /// <summary>Reveals (x,y) and, if it is a 0, cascades to its neighbors exactly like a real click.
    /// Returns how many previously-hidden cells just became revealed.</summary>
    private static int RevealFlood(Board board, CellState[,] state, int x, int y)
    {
        if (state[x, y] != CellState.Hidden) return 0;
        int revealed = 1;
        state[x, y] = CellState.Revealed;
        var queue = new Queue<(int, int)>();
        queue.Enqueue((x, y));
        while (queue.Count > 0)
        {
            var (cx, cy) = queue.Dequeue();
            if (board.AdjacentMines(cx, cy) != 0) continue;
            foreach (var (nx, ny) in board.Neighbors(cx, cy))
            {
                if (state[nx, ny] != CellState.Hidden) continue;
                state[nx, ny] = CellState.Revealed;
                revealed++;
                queue.Enqueue((nx, ny));
            }
        }
        return revealed;
    }

    private static IEnumerable<(int, int)> NeighborsOf(int x, int y, int width, int height)
    {
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && nx < width && ny >= 0 && ny < height) yield return (nx, ny);
            }
    }
}
