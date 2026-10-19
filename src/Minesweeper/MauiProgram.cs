using AdsService;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Minesweeper.Core.Licensing;
using Minesweeper.Services;
using Minesweeper.Views;

namespace Minesweeper;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		// Same AdsMode (Dev/Pro) split as PdfReader/DocScanner: default "Dev" always requests the sandbox/
		// test ad units, so a Release .aab uploaded to a Play testing track can never show a real ad.
#if ADS_MODE_PRO
		bool useTestAds = !RealAdsConfig.HasRealIds;
#else
		bool useTestAds = true;
#endif

		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			// CommunityToolkit.Maui's TouchBehavior: the only reliable cross-platform way to detect a
			// long-press in stock MAUI (no built-in LongPressGestureRecognizer) -- used on each board cell
			// for "long-press to flag" (GamePage.xaml.cs).
			.UseMauiCommunityToolkit()
			.UseLevelPlayAds(new LevelPlayOptions(
				AppKey: LevelPlayConfig.AppKey,
				BannerAdUnitId: useTestAds ? LevelPlayConfig.BannerAdUnitId : RealAdsConfig.BannerAdUnitId,
				InterstitialAdUnitId: useTestAds ? LevelPlayConfig.InterstitialAdUnitId : RealAdsConfig.InterstitialAdUnitId,
				TestMode: LevelPlayConfig.TestMode)
			{
#if ADS_DISABLED
				IsAdsEnabled = false,
#else
				IsAdsEnabled = true,
#endif
			})
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
				fonts.AddFont("MaterialIcons-Regular.ttf", "Icons");
			});

		builder.Services.AddSingleton<GameStatsStore>();
		builder.Services.AddSingleton<SavedGameStore>();
		builder.Services.AddSingleton<LevelProgressStore>();
		builder.Services.AddSingleton<ILicenseService, LicenseService>();
		// Fully qualified: the "AdsService" namespace (the ads library) and this app's own
		// "Minesweeper.Services.AdsService" class share a name, so unqualified "AdsService" here is ambiguous.
		builder.Services.AddSingleton<IAdsService, Minesweeper.Services.AdsService>();

		builder.Services.AddTransient<HomePage>();
		builder.Services.AddTransient<GamePage>();
		builder.Services.AddTransient<LevelsPage>();
		builder.Services.AddTransient<SettingsPage>();
		builder.Services.AddTransient<AboutPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		MauiApp app = builder.Build();
		// Resolve the ads service now (before any page exists): its constructor applies the cached Pro flag
		// to the ads library, which creates the ad SDK lazily -- a Pro user therefore never initialises the
		// SDK. Same reasoning as PdfReader/DocScanner's own MauiProgram.
		_ = app.Services.GetRequiredService<IAdsService>();
		return app;
	}
}
