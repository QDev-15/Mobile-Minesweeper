namespace Minesweeper.Core;

/// <summary>1-3 star rating for a completed Tutorial level, based on completion time (plan: "mỗi level sẽ
/// tính đánh giá 1-2-3 sao theo thời gian hoàn thành"). The "par" time below is a first-pass heuristic (cells
/// cleared per second for a reasonably careful player), not tuned against real players -- there is no
/// playtesting data to calibrate it against yet; revisit once real completion times are observed. Loosened
/// 2026-10-21 (owner's report: 3 stars was too tight to realistically reach) -- both a slower assumed pace
/// and a more generous 3-star cutoff (all of par, not half of it).</summary>
public static class LevelStars
{
    /// <summary>3 stars at or under par itself, 2 stars at or under 1.6x par, 1 star for any completion
    /// slower than that (finishing a level always earns at least 1 star).</summary>
    public static int StarsFor(int level, int elapsedSeconds)
    {
        int par = ParSeconds(LevelCatalog.DifficultyFor(level));
        if (elapsedSeconds <= par) return 3;
        if (elapsedSeconds <= par * 1.6) return 2;
        return 1;
    }

    /// <summary>~1.5 safe cells cleared per second (down from 2.5), floor of 20s so a tiny level 1 board
    /// still gives the player a comfortable window for 3 stars.</summary>
    private static int ParSeconds(Difficulty d)
    {
        int safeCells = d.Width * d.Height - d.MineCount;
        return Math.Max(20, (int)Math.Round(safeCells / 1.5));
    }
}
