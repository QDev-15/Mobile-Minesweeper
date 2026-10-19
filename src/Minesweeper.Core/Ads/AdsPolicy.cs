namespace Minesweeper.Core.Ads;

public sealed record AdsState(DateTimeOffset? LastShownUtc)
{
    public static readonly AdsState Initial = new((DateTimeOffset?)null);
}

/// <summary>Interstitial rule for Dò Mìn, same shape as PdfReader/DocScanner's own AdsPolicy: at most once
/// per hour, shown only at a natural transition point -- here, right after a game ends (win or lose), never
/// mid-game. Pure data + pure logic (no ad SDK); only actually loading/showing an ad talks to Android
/// (Services/AdsService.cs).</summary>
public static class AdsPolicy
{
    public static readonly TimeSpan MinInterval = TimeSpan.FromHours(1);

    /// <summary>Call right after a game ends (Won or Lost), before the player starts another one. Returns
    /// the state to persist and whether an interstitial should be shown now. Never true when
    /// <paramref name="isPro"/>.</summary>
    public static (AdsState Next, bool ShowInterstitial) AtGameEnd(AdsState state, bool isPro, DateTimeOffset nowUtc)
    {
        if (isPro) return (state, false);
        bool due = state.LastShownUtc is null || nowUtc - state.LastShownUtc.Value >= MinInterval;
        return due ? (new AdsState(nowUtc), true) : (state, false);
    }
}
