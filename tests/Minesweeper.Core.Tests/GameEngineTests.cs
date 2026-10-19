using Minesweeper.Core;

namespace Minesweeper.Core.Tests;

public class GameEngineTests
{
    /// <summary>3x3 board, single mine at (2,2) (bottom-right corner): clicking (0,0) must flood-reveal the
    /// whole rest of the board in one go, since none of the other 8 cells touch the mine except the 3
    /// bordering it, which should surface as the flood's boundary.</summary>
    private static Board SingleCornerMineBoard()
    {
        var mines = new bool[3, 3];
        mines[2, 2] = true;
        return new Board(3, 3, mines);
    }

    [Fact]
    public void Reveal_Mine_LosesAndExposesAllMines()
    {
        var engine = new GameEngine(SingleCornerMineBoard());
        engine.Reveal(2, 2);
        Assert.Equal(GameStatus.Lost, engine.Status);
        Assert.Equal(CellState.Revealed, engine.StateAt(2, 2));
    }

    [Fact]
    public void Reveal_LastSafeCell_Wins()
    {
        var engine = new GameEngine(SingleCornerMineBoard());
        engine.Reveal(0, 0); // floods the entire safe region (1 mine only) in one click on this tiny board
        Assert.Equal(GameStatus.Won, engine.Status);
        Assert.Equal(CellState.Hidden, engine.StateAt(2, 2)); // the mine itself is never auto-revealed on a win
    }

    [Fact]
    public void ToggleFlag_TracksMinesRemaining()
    {
        var engine = new GameEngine(SingleCornerMineBoard());
        Assert.Equal(1, engine.MinesRemaining);
        engine.ToggleFlag(2, 2);
        Assert.Equal(CellState.Flagged, engine.StateAt(2, 2));
        Assert.Equal(0, engine.MinesRemaining);
        engine.ToggleFlag(2, 2);
        Assert.Equal(CellState.Hidden, engine.StateAt(2, 2));
        Assert.Equal(1, engine.MinesRemaining);
    }

    [Fact]
    public void Chord_RevealsHiddenNeighborsWhenFlagCountMatches()
    {
        // 2x2, mine at (0,0): every other cell touches it directly (adjacency 1, nothing is a "0" cell), so
        // a single Reveal can never cascade here -- only Chord, after flagging the mine, can open the rest.
        var mines = new bool[2, 2];
        mines[0, 0] = true;
        var engine = new GameEngine(new Board(2, 2, mines));

        engine.Reveal(1, 1);
        Assert.Equal(GameStatus.Playing, engine.Status); // confirms the no-cascade premise above
        Assert.Equal(CellState.Hidden, engine.StateAt(1, 0));
        Assert.Equal(CellState.Hidden, engine.StateAt(0, 1));

        engine.ToggleFlag(0, 0);
        engine.Chord(1, 1);

        Assert.Equal(CellState.Revealed, engine.StateAt(1, 0));
        Assert.Equal(CellState.Revealed, engine.StateAt(0, 1));
        Assert.Equal(GameStatus.Won, engine.Status);
    }

    [Fact]
    public void FindHint_ReturnsNull_WhenNothingYetProvable()
    {
        var engine = new GameEngine(SingleCornerMineBoard());
        // Nothing revealed yet -- no constraints exist at all, so no hint is derivable.
        Assert.Null(engine.FindHint());
    }

    [Fact]
    public void Restore_ReproducesExactState_IncludingMinesRemainingAndStatus()
    {
        // 3x3, mine at (2,0) only (same layout as the Chord test's board shape elsewhere): play partway --
        // reveal a corner, flag the mine -- then round-trip through Restore and confirm everything matches,
        // the way a save/resume cycle would (Services/SavedGameStore.cs in the app).
        var mines = new bool[3, 3];
        mines[2, 0] = true;
        var board = new Board(3, 3, mines);
        var original = new GameEngine(board);
        original.Reveal(0, 2);
        original.ToggleFlag(2, 0);

        var snapshot = new CellState[3, 3];
        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                snapshot[x, y] = original.StateAt(x, y);

        GameEngine restored = GameEngine.Restore(board, snapshot, original.Status);

        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                Assert.Equal(original.StateAt(x, y), restored.StateAt(x, y));
        Assert.Equal(original.MinesRemaining, restored.MinesRemaining);
        Assert.Equal(original.Status, restored.Status);

        // And it is still a live, playable engine afterward, not a frozen read-only copy.
        restored.ToggleFlag(2, 0);
        Assert.Equal(CellState.Hidden, restored.StateAt(2, 0));
    }
}
