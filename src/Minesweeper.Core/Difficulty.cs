namespace Minesweeper.Core;

public enum DifficultyLevel { Easy, Medium, Hard }

/// <summary>3 difficulties, all the SAME board size now -- 12 wide (the column cap every difficulty uses,
/// GamePage.BuildEmptyBoard) by 24 tall, identical to DailyChallenge's own board (owner's direction,
/// 2026-10-21: "số ô thì full màn hình như màn chơi hàng ngày" -- Easy/Medium/Hard should fill the screen
/// the same way Daily does, not use 3 different sizes). Difficulty is purely mine count now: 20/50/99,
/// also the owner's own numbers -- Medium ends up mechanically identical to DailyChallenge.Difficulty
/// (12x24/50), which is intentional, not a coincidence to resolve.</summary>
public sealed record Difficulty(string Name, int Width, int Height, int MineCount)
{
    public static readonly Difficulty Easy = new("Dễ", 12, 24, 20);
    public static readonly Difficulty Medium = new("Vừa", 12, 24, 50);
    public static readonly Difficulty Hard = new("Khó", 12, 24, 99);

    public static Difficulty For(DifficultyLevel level) => level switch
    {
        DifficultyLevel.Easy => Easy,
        DifficultyLevel.Medium => Medium,
        DifficultyLevel.Hard => Hard,
        _ => throw new ArgumentOutOfRangeException(nameof(level)),
    };
}
