namespace Minesweeper.Core;

public enum GameStatus { Playing, Won, Lost }

/// <summary>Player-facing game state machine over a generated <see cref="Board"/>: reveal, flag, chord, win/
/// loss. The MAUI app's game page is a thin renderer over this -- all rules live here so they stay unit
/// testable on the PC (plan section 2).</summary>
public sealed class GameEngine
{
    private readonly Board _board;
    private readonly CellState[,] _state;
    private readonly int _totalSafeCells;
    private int _revealedSafeCount;

    public int Width => _board.Width;
    public int Height => _board.Height;
    public GameStatus Status { get; private set; } = GameStatus.Playing;
    public int MineCount { get; }
    public int MinesRemaining { get; private set; }

    /// <summary>Exposed so a caller can serialize the exact mine layout for save/resume (Services/
    /// SavedGameStore.cs in the app) -- a save must restore the identical board, not merely a new one of
    /// the same size.</summary>
    public Board Board => _board;

    public GameEngine(Board board)
    {
        _board = board;
        _state = new CellState[board.Width, board.Height];
        MineCount = board.MineCount();
        _totalSafeCells = board.Width * board.Height - MineCount;
        MinesRemaining = MineCount;
    }

    /// <summary>Rebuilds a <see cref="GameEngine"/> from a previously saved per-cell state (save/resume):
    /// <see cref="MinesRemaining"/> and the internal safe-reveal count are recomputed from
    /// <paramref name="state"/> itself, not stored separately, so a restored engine can never drift from
    /// what the saved cells actually say.</summary>
    public static GameEngine Restore(Board board, CellState[,] state, GameStatus status)
    {
        var engine = new GameEngine(board);
        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                CellState cell = state[x, y];
                engine._state[x, y] = cell;
                if (cell == CellState.Revealed && !board.IsMine(x, y)) engine._revealedSafeCount++;
                else if (cell == CellState.Flagged) engine.MinesRemaining--;
            }
        }
        engine.Status = status;
        return engine;
    }

    public CellState StateAt(int x, int y) => _state[x, y];

    /// <summary>-1 at a mine (only meaningful once that cell is Revealed, i.e. the losing cell or one of the
    /// mines auto-exposed on loss); 0-8 everywhere else. Do not read while the cell is still Hidden/Flagged.</summary>
    public int NumberAt(int x, int y) => _board.AdjacentMines(x, y);

    public void Reveal(int x, int y)
    {
        if (Status != GameStatus.Playing || _state[x, y] != CellState.Hidden) return;

        if (_board.IsMine(x, y))
        {
            _state[x, y] = CellState.Revealed;
            Status = GameStatus.Lost;
            RevealAllMines();
            return;
        }

        RevealFlood(x, y);
        if (_revealedSafeCount >= _totalSafeCells) Status = GameStatus.Won;
    }

    public void ToggleFlag(int x, int y)
    {
        if (Status != GameStatus.Playing) return;
        switch (_state[x, y])
        {
            case CellState.Hidden: _state[x, y] = CellState.Flagged; MinesRemaining--; break;
            case CellState.Flagged: _state[x, y] = CellState.Hidden; MinesRemaining++; break;
        }
    }

    /// <summary>Chord: tap an already-revealed number whose adjacent flag count already matches its number,
    /// to reveal all its other hidden neighbors at once -- the classic two-button-mouse shortcut, remapped
    /// for touch (plan section 5: double-tap). A wrong flag among those neighbors loses the game exactly like
    /// the real rule; this never silently refuses just because a flag might be wrong.</summary>
    public void Chord(int x, int y)
    {
        if (Status != GameStatus.Playing || _state[x, y] != CellState.Revealed) return;
        int number = _board.AdjacentMines(x, y);
        if (number <= 0) return;

        int flagged = 0;
        var hidden = new List<(int, int)>();
        foreach (var (nx, ny) in _board.Neighbors(x, y))
        {
            if (_state[nx, ny] == CellState.Flagged) flagged++;
            else if (_state[nx, ny] == CellState.Hidden) hidden.Add((nx, ny));
        }
        if (flagged != number) return;

        foreach (var (nx, ny) in hidden) Reveal(nx, ny);
    }

    /// <summary>One cell the player can currently prove safe from exactly what they see right now (not from
    /// the generator's whole-board proof at creation time) -- the Hint feature (plan section 4, Tier 2). Null
    /// when nothing is provably safe yet from the current state, even on a board that was confirmed No-guess
    /// solvable overall: that proof can need information from cells the player has not revealed yet.</summary>
    public (int X, int Y)? FindHint()
    {
        if (Status != GameStatus.Playing) return null;
        var d = NoGuessSolver.DeduceOnePass(_state, _board.AdjacentMines, Width, Height, MineCount);
        return d.Safe.Count > 0 ? d.Safe[0] : null;
    }

    private void RevealFlood(int startX, int startY)
    {
        var queue = new Queue<(int, int)>();
        queue.Enqueue((startX, startY));
        _state[startX, startY] = CellState.Revealed;
        _revealedSafeCount++;
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            if (_board.AdjacentMines(x, y) != 0) continue;
            foreach (var (nx, ny) in _board.Neighbors(x, y))
            {
                if (_state[nx, ny] != CellState.Hidden) continue;
                _state[nx, ny] = CellState.Revealed;
                _revealedSafeCount++;
                queue.Enqueue((nx, ny));
            }
        }
    }

    private void RevealAllMines()
    {
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                if (_board.IsMine(x, y) && _state[x, y] == CellState.Hidden) _state[x, y] = CellState.Revealed;
    }
}
