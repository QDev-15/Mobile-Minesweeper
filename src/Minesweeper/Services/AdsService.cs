using Minesweeper.Core.Ads;
using Minesweeper.Core.Licensing;
using AdsClient = AdsService.IAdsClient;

namespace Minesweeper.Services;

/// <summary>Whether ads should be visible right now, and the one place in the app that applies "Pro" + the
/// once-per-hour-at-a-natural-transition cadence rule (plan section 6) -- mirrors <see cref="LicenseService"/>
/// being the one place that talks to Play Billing. The interstitial rule itself lives in
/// <see cref="AdsPolicy"/> and is unit-tested on its own; the actual ad network (LevelPlay) lives entirely in
/// the reusable <c>AdsService</c> library, copied unchanged from PdfReader (2026-10-17).</summary>
public interface IAdsService
{
    bool ShowAds { get; }
    bool IsBannerLoaded { get; }
    event Action? Changed;

    /// <summary>Call right after a game ends (Won or Lost), before the player starts another one or leaves
    /// the game page -- never while a board is still being played. Shows an interstitial when
    /// <see cref="AdsPolicy"/> says it is due and one happens to be ready; otherwise this cycle is skipped
    /// quietly.</summary>
    void RegisterGameEnd();
}

public sealed class AdsService : IAdsService
{
    private const string StateKey = "ads_last_interstitial_utc_ticks";

    private readonly ILicenseService _license;
    private readonly AdsClient _ads;

    // ADS_DISABLED (Minesweeper.csproj's AdsEnabled=false MSBuild property): treat as Pro so every
    // Pro-gated branch below already does the right thing with no further changes (same trick as
    // PdfReader's AdsService).
    private bool IsProOrAdsDisabled =>
#if ADS_DISABLED
        true;
#else
        _license.State.IsPro;
#endif

    public AdsService(ILicenseService license, AdsClient ads)
    {
        _license = license;
        _ads = ads;
        _ads.AreAdsEnabled = !IsProOrAdsDisabled;
        _license.Changed += () =>
        {
            _ads.AreAdsEnabled = !IsProOrAdsDisabled;
            Changed?.Invoke();
        };
        _ads.BannerStateChanged += () => Changed?.Invoke();
        _ads.PrepareInterstitial();
    }

    public bool ShowAds => !IsProOrAdsDisabled;
    public bool IsBannerLoaded => _ads.IsBannerLoaded;
    public event Action? Changed;

    public void RegisterGameEnd()
    {
        long ticks = Preferences.Default.Get(StateKey, 0L);
        var state = new AdsState(ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero));
        (AdsState next, bool show) = AdsPolicy.AtGameEnd(state, IsProOrAdsDisabled, DateTimeOffset.UtcNow);
        Preferences.Default.Set(StateKey, next.LastShownUtc?.UtcTicks ?? 0L);

        if (show) _ads.ShowInterstitialIfReady();
        _ads.PrepareInterstitial();
    }
}
