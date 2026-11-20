namespace Minesweeper.Core;

public enum DifficultyLevel { Easy, Medium, Hard }

/// <summary>3 difficulties, all the SAME board size now -- 12 wide (the column cap every difficulty uses,
/// GamePage.BuildEmptyBoard) by 24 tall, identical to DailyChallenge's own board (owner's direction,
/// 2026-10-21: "số ô thì full màn hình như màn chơi hàng ngày" -- Easy/Medium/Hard should fill the screen
/// the same way Daily does, not use 3 different sizes). Difficulty is purely mine count now: 20/50/60 --
/// Medium ends up mechanically identical to DailyChallenge.Difficulty (12x24/50), which is intentional, not
/// a coincidence to resolve.
///
/// Hard was 99 mines (34.4% density, well above classic Expert's 20.6%) until the owner reported it was
/// unwinnable, 2026-11-19 -- GamePage also ran it WITHOUT No-guess (the only difficulty that didn't
/// guarantee a logic-only solution), so a player could be forced into a genuine blind guess against that
/// density with no recourse (not even GameEngine.FindHint, which only finds logically-deducible safe cells).
/// Lowered to 60/288 (20.8%) -- the exact density LevelCatalog.MaxDensity already measured as the reliable
/// ceiling for No-guess generation (0/10 failures; 65 already reads as "close to the cliff" in that
/// measurement, 70 failed 2/10) -- specifically so GamePage could also turn No-guess BACK ON for Hard (see
/// its own comment) without board generation becoming slow or unreliable. Hard and Tutorial's level 100 are
/// now the exact same board (12x24/60) by construction, not a coincidence to resolve either -- see
/// LevelCatalogTests.</summary>
public sealed record Difficulty(string Name, int Width, int Height, int MineCount)
{
    public static readonly Difficulty Easy = new("Dễ", 12, 24, 20);
    public static readonly Difficulty Medium = new("Vừa", 12, 24, 50);
    public static readonly Difficulty Hard = new("Khó", 12, 24, 60);

    public static Difficulty For(DifficultyLevel level) => level switch
    {
        DifficultyLevel.Easy => Easy,
        DifficultyLevel.Medium => Medium,
        DifficultyLevel.Hard => Hard,
        _ => throw new ArgumentOutOfRangeException(nameof(level)),
    };
}
