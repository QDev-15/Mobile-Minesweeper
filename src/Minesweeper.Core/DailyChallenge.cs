namespace Minesweeper.Core;

/// <summary>Daily Challenge's own dedicated board size (not Difficulty.Medium/Hard) -- 12 wide (same column
/// cap every difficulty uses, GamePage.BuildEmptyBoard), 24 tall and 50 mines (owner's direction, 2026-10-20)
/// so the board actually fills the screen's full height instead of leaving a gap above/below like Hard's
/// (12x16) own board does. No longer a fixed, date-seeded board (that was reported as a serious bug,
/// 2026-10-21: it opened around a fixed center cell regardless of where the player tapped, and stayed
/// identical across every replay) -- GamePage generates a fresh random board at this size per play, through
/// the exact same path as every other difficulty (GamePage.StartGameFromAsync).</summary>
public static class DailyChallenge
{
    public static readonly Difficulty Difficulty = new("Hôm nay", 12, 24, 50);
}
