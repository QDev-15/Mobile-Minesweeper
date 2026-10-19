namespace Minesweeper;

/// <summary>Unity LevelPlay settings. Minesweeper does not have its own LevelPlay app yet (MINESWEEPER-PLAN.md
/// section 7) -- these are placeholders, same shape as PdfReader's/DocScanner's own LevelPlayConfig.cs.
/// Until a real AppKey is filled in, ads simply never load (LevelPlay rejects an empty app key, the banner/
/// interstitial stay hidden forever) -- the app still runs correctly, it just shows no ads, which is a safe
/// default to ship M0-M3 with before M4 (plan section 8) sets up the real LevelPlay app.</summary>
public static class LevelPlayConfig
{
    public const string AppKey = "REPLACE_ME_LEVELPLAY_APP_KEY";
    public const string BannerAdUnitId = "REPLACE_ME_BANNER_SANDBOX";
    public const string InterstitialAdUnitId = "REPLACE_ME_INTERSTITIAL_SANDBOX";

#if DEBUG
    public const bool TestMode = true;
#else
    public const bool TestMode = false;
#endif
}
