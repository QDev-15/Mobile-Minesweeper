namespace Minesweeper.Core;

/// <summary>What the player currently sees at a cell -- never carries mine truth itself (that stays in
/// <see cref="Board"/>), so this is the exact same shape the solver reasons over and a real player sees.</summary>
public enum CellState { Hidden, Revealed, Flagged }
