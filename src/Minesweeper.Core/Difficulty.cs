namespace Minesweeper.Core;

public enum DifficultyLevel { Easy, Medium, Hard }

/// <summary>The 3 classic board sizes (plan section 4, Tier 1) -- same proportions as the original Windows
/// Minesweeper (Beginner/Intermediate/Expert), kept because they are the ones most players already know by
/// feel.</summary>
public sealed record Difficulty(string Name, int Width, int Height, int MineCount)
{
    public static readonly Difficulty Easy = new("Dễ", 9, 9, 10);
    public static readonly Difficulty Medium = new("Vừa", 16, 16, 40);
    public static readonly Difficulty Hard = new("Khó", 30, 16, 99);

    public static Difficulty For(DifficultyLevel level) => level switch
    {
        DifficultyLevel.Easy => Easy,
        DifficultyLevel.Medium => Medium,
        DifficultyLevel.Hard => Hard,
        _ => throw new ArgumentOutOfRangeException(nameof(level)),
    };
}
