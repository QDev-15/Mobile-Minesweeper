namespace Minesweeper;

/// <summary>Production ad unit IDs, used only once a build is made with <c>-p:AdsMode=Pro</c> and both ids
/// here no longer contain "REPLACE_ME" (MINESWEEPER-PLAN.md section 7). Same shape as PdfReader's/
/// DocScanner's own RealAdsConfig.cs.</summary>
public static class RealAdsConfig
{
    public const string BannerAdUnitId = "REPLACE_ME_BANNER";
    public const string InterstitialAdUnitId = "REPLACE_ME_INTERSTITIAL";

    public static bool HasRealIds => !BannerAdUnitId.Contains("REPLACE_ME") && !InterstitialAdUnitId.Contains("REPLACE_ME");
}
