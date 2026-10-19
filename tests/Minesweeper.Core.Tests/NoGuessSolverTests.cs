using Minesweeper.Core;

namespace Minesweeper.Core.Tests;

/// <summary>Tests <see cref="NoGuessSolver.DeduceOnePass"/> directly against hand-built, fully controlled
/// scenarios (not through <see cref="Board"/>/flood-fill geometry) -- these are the ground-truth proof that
/// the deduction rules are correct, independent of how a real board happens to lay out.</summary>
public class NoGuessSolverTests
{
    private const int W = 4, H = 4;

    private static CellState[,] EmptyState() => new CellState[W, H];

    [Fact]
    public void BasicRule_RemainingEqualsHiddenCount_AllAreMines()
    {
        var state = EmptyState();
        state[1, 1] = CellState.Revealed; // the "1"
        // Only one hidden neighbor: (2,1). Everything else around it pre-revealed so it is the sole hidden cell.
        foreach (var (x, y) in Around(1, 1)) state[x, y] = CellState.Revealed;
        state[2, 1] = CellState.Hidden;

        var d = NoGuessSolver.DeduceOnePass(state, (x, y) => x == 1 && y == 1 ? 1 : 0, W, H);

        Assert.Empty(d.Safe);
        Assert.Contains((2, 1), d.Mines);
    }

    [Fact]
    public void BasicRule_RemainingZeroAfterFlags_AllHiddenAreSafe()
    {
        var state = EmptyState();
        state[1, 1] = CellState.Revealed; // the "1", already satisfied by one flag
        foreach (var (x, y) in Around(1, 1)) state[x, y] = CellState.Revealed;
        state[2, 1] = CellState.Flagged;
        state[0, 0] = CellState.Hidden; // another hidden neighbor of (1,1)

        var d = NoGuessSolver.DeduceOnePass(state, (x, y) => x == 1 && y == 1 ? 1 : 0, W, H);

        Assert.Contains((0, 0), d.Safe);
        Assert.Empty(d.Mines);
    }

    [Fact]
    public void TwoHiddenNeighborsOneMine_IsGenuinelyAmbiguous_NoDeduction()
    {
        // The textbook 50/50: a "1" with exactly 2 hidden neighbors and nothing else touching either of
        // them. No valid logical rule can tell which one is the mine -- a real No-guess generator must
        // reject this board, which only holds if the solver reports nothing provable here.
        var state = EmptyState();
        state[1, 1] = CellState.Revealed;
        foreach (var (x, y) in Around(1, 1)) state[x, y] = CellState.Revealed;
        state[2, 1] = CellState.Hidden;
        state[0, 0] = CellState.Hidden;

        var d = NoGuessSolver.DeduceOnePass(state, (x, y) => x == 1 && y == 1 ? 1 : 0, W, H);

        Assert.Empty(d.Safe);
        Assert.Empty(d.Mines);
    }

    [Fact]
    public void SubsetElimination_ResolvesWhatBasicRuleCannot()
    {
        // 3x2 grid. (0,0)="1" touches hidden {(0,1),(1,1)} (A, remaining 1 of 2 -- alone, ambiguous).
        // (1,0)="2" touches hidden {(0,1),(1,1),(2,1)} (B, remaining 2 of 3 -- alone, ambiguous too).
        // A.Cells is a literal subset of B.Cells, so B\A = {(2,1)} must carry exactly 2-1=1 mine on its
        // own -> (2,1) is a mine. Only subset elimination reaches this, not the basic rule on either alone.
        const int w = 3, h = 2;
        var state = new CellState[w, h];
        var numbers = new Dictionary<(int, int), int> { [(0, 0)] = 1, [(1, 0)] = 2, [(2, 0)] = 0 };
        state[0, 0] = CellState.Revealed;
        state[1, 0] = CellState.Revealed;
        state[2, 0] = CellState.Revealed;
        state[0, 1] = CellState.Hidden;
        state[1, 1] = CellState.Hidden;
        state[2, 1] = CellState.Hidden;

        var d = NoGuessSolver.DeduceOnePass(state, (x, y) => numbers.GetValueOrDefault((x, y)), w, h);

        Assert.Contains((2, 1), d.Mines);
        Assert.Empty(d.Safe);
    }

    private static IEnumerable<(int, int)> Around(int x, int y)
    {
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && nx < W && ny >= 0 && ny < H) yield return (nx, ny);
            }
    }
}
