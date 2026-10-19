using System.Text.Json;
using Minesweeper.Core;

namespace Minesweeper.Services;

/// <summary>What GamePage needs to fully reconstruct an in-progress board: the exact mine layout (not just
/// width/height/mine count -- a new board would be a different puzzle), every cell's current state, and
/// enough about how the game was started to show the right UI on resume.</summary>
public sealed record SavedGame(
    int Width,
    int Height,
    bool[] MinesFlat,
    int[] StateFlat,
    int Status,
    int ElapsedSeconds,
    bool IsDaily,
    bool NoGuess,
    int DifficultyLevel,
    int LevelNumber, // 0 = not Tutorial level mode; 1-100 = Tutorial level (LevelCatalog)
    string SavedDateUtc);

/// <summary>Local-only save slot for one in-progress game (plan: exit confirmation + resume). A single slot,
/// not a history -- same "no account, everything stays on the phone" stance as the rest of the app. Stored
/// as one JSON string via <see cref="Preferences"/> (same mechanism as every other small piece of persisted
/// state in this app and the sibling repos); a real database would be overkill for one record.</summary>
public sealed class SavedGameStore
{
    private const string Key = "saved_game_json";

    public bool HasSavedGame => Preferences.Default.ContainsKey(Key);

    public SavedGame? Load()
    {
        string? json = Preferences.Default.Get(Key, (string?)null);
        if (json == null) return null;
        try { return JsonSerializer.Deserialize<SavedGame>(json); }
        catch (JsonException) { return null; } // corrupt/old-format save: treat as none rather than crash
    }

    public void Save(GameEngine engine, int elapsedSeconds, bool isDaily, bool noGuess, DifficultyLevel level, int levelNumber)
    {
        int width = engine.Width, height = engine.Height;
        var mines = new bool[width * height];
        var states = new int[width * height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int i = x * height + y;
                mines[i] = engine.Board.IsMine(x, y);
                states[i] = (int)engine.StateAt(x, y);
            }
        }

        var save = new SavedGame(width, height, mines, states, (int)engine.Status, elapsedSeconds,
            isDaily, noGuess, (int)level, levelNumber, DateTime.UtcNow.ToString("O"));
        Preferences.Default.Set(Key, JsonSerializer.Serialize(save));
    }

    public void Clear() => Preferences.Default.Remove(Key);

    /// <summary>Rebuilds the live <see cref="GameEngine"/> from a loaded <see cref="SavedGame"/>.</summary>
    public static GameEngine ToEngine(SavedGame save)
    {
        var mines2d = new bool[save.Width, save.Height];
        var state2d = new CellState[save.Width, save.Height];
        for (int x = 0; x < save.Width; x++)
        {
            for (int y = 0; y < save.Height; y++)
            {
                int i = x * save.Height + y;
                mines2d[x, y] = save.MinesFlat[i];
                state2d[x, y] = (CellState)save.StateFlat[i];
            }
        }
        var board = new Board(save.Width, save.Height, mines2d);
        return GameEngine.Restore(board, state2d, (GameStatus)save.Status);
    }
}
