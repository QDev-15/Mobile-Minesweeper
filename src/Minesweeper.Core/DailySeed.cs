namespace Minesweeper.Core;

/// <summary>Turns a calendar date into a deterministic board seed, so every player opening the app on the
/// same UTC date gets the exact same Daily Challenge board (plan section 3.3) -- no server needed, just
/// agreement that "today" means UTC. Uses FNV-1a, not <c>string.GetHashCode()</c>: .NET's own string hash is
/// randomized per process (documented, security-motivated) and would give each device a different board.</summary>
public static class DailySeed
{
    public static int For(DateOnly date, DifficultyLevel level)
    {
        string key = $"{date:yyyy-MM-dd}|{level}";
        uint hash = 2166136261;
        foreach (char c in key)
        {
            hash ^= c;
            hash *= 16777619;
        }
        return unchecked((int)hash);
    }
}
