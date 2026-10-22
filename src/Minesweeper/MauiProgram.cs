using AdsService;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using Minesweeper.Core;
using Minesweeper.Core.Licensing;
using Minesweeper.Services;
using Minesweeper.Views;
using Plugin.Maui.Audio;

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
			// Plugin.Maui.Audio: registers IAudioManager for SoundService (gameplay sound effects).
			.AddAudio()
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
				// Variable font (single file, no separate weight files published upstream for this family) —
				// renders at its default instance; "trắng sữa" redesign 2026-10-19 uses it for Display/Title
				// role text (wordmark, page titles, big numerals), OFL-licensed (Resources/Fonts/Fraunces-OFL.txt).
				fonts.AddFont("Fraunces-Variable.ttf", "Fraunces");
			});

		builder.Services.AddSingleton<GameStatsStore>();
		builder.Services.AddSingleton<SavedGameStore>();
		builder.Services.AddSingleton<LevelProgressStore>();
		builder.Services.AddSingleton<ILicenseService, LicenseService>();
		builder.Services.AddSingleton<ISoundService, SoundService>();
		builder.Services.AddSingleton<DailyProgressStore>();
		// Fully qualified: the "AdsService" namespace (the ads library) and this app's own
		// "Minesweeper.Services.AdsService" class share a name, so unqualified "AdsService" here is ambiguous.
		builder.Services.AddSingleton<IAdsService, Minesweeper.Services.AdsService>();

		builder.Services.AddTransient<HomePage>();
		builder.Services.AddTransient<GamePage>();
		builder.Services.AddTransient<LevelsPage>();
		builder.Services.AddTransient<DailyCalendarPage>();
		builder.Services.AddTransient<SettingsPage>();
		builder.Services.AddTransient<AboutPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		MauiApp app = builder.Build();
		// Resolve the ads service (its constructor applies the cached Pro flag to the ads library, and for a
		// non-Pro user that synchronously constructs + Initialize()s the LevelPlay SDK -- real class-loading/
		// JNI cost, not just a cheap flag check). Resolving it right here inline used to run that cost as part
		// of CreateMauiApp(), which Android calls from MauiApplication.OnCreate() -- i.e. it blocked the app's
		// own OnCreate from returning, before MainActivity/the first page/frame even starts. Posting it via
		// MainThread instead keeps the "before any page can request a banner/interstitial" guarantee (same
		// reasoning as PdfReader/DocScanner's own MauiProgram) while letting Android's OnCreate return and the
		// first frame get drawn first -- fixed 2026-10-20 after the owner reported slow app startup.
		MainThread.BeginInvokeOnMainThread(() => _ = app.Services.GetRequiredService<IAdsService>());

		// Warms up the JIT for BoardGenerator/NoGuessSolver off the critical path (owner's report,
		// 2026-10-21): a Debug/JIT session only compiles a method's native code the first time it actually
		// runs, so the player's very first tap of a fresh app session paid that cost on top of real
		// generation time -- a throwaway tiny board on a background thread, started now while the player is
		// still looking at HomePage, exercises the exact same code paths so the real first tap doesn't have
		// to. A Release/AOT build (what Play Store ships) compiles ahead of time and barely needs this, but
		// it is harmless there too -- a 9x9 board generates in low single-digit milliseconds either way.
		_ = Task.Run(() => BoardGenerator.GenerateNoGuess(Difficulty.Easy, 4, 4, new Random()));

		// REVERTED 2026-10-22: tried pre-warming a throwaway GamePage instance here (same dispatched-off-
		// CreateMauiApp() idea as the ads warm-up above) to pay its first-construction cost early instead of
		// on the player's first "Chơi" tap. Caused an immediate crash on a real device instead -- almost
		// certainly because constructing a ContentPage this early races the platform's own App/Window/Shell
		// startup sequence: CreateMauiApp() only builds the DI container, the actual App instance (and with
		// it Application.Current.Resources, which GamePage.xaml's `{StaticResource Card}` etc. need to
		// already be merged) is not guaranteed to exist yet by the time a MainThread.BeginInvokeOnMainThread
		// callback queued this early actually runs -- unlike the IAdsService warm-up above, whose constructor
		// touches no XAML/UI resources at all. Not worth the crash risk for an unverified perf tweak; left as
		// a note so this exact approach isn't tried again the same way.
		return app;
	}
}
