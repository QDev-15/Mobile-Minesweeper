using Minesweeper.Core.Licensing;
using Minesweeper.Services;
using Minesweeper.Views;

namespace Minesweeper;

public partial class App : Application
{
	// "System" (default) follows RequestedTheme/RequestedThemeChanged like before 2026-10-19; "Light"/"Dark"/
	// "HighContrast" are a manual override chosen from SettingsPage (design spec §1.4/§3.6) and persisted so
	// it survives a restart, same Preferences.Default pattern as every other *Store class in this app.
	private const string ThemeOverrideKey = "ThemeOverride";
	public const string ThemeSystem = "System";
	public const string ThemeLight = "Light";
	public const string ThemeDark = "Dark";
	public const string ThemeHighContrast = "HighContrast";

	public App()
	{
		// Before InitializeComponent: that call (and everything after it) can construct XAML content whose
		// {x:Static strings:AppStrings.X} bindings resolve through CultureInfo.CurrentUICulture, so the right
		// language has to already be in effect before any of that runs, not after.
		AppLanguage.Apply();
		InitializeComponent();
		ApplyColorTheme();
		RequestedThemeChanged += (_, _) =>
		{
			if (ThemeOverride == ThemeSystem) ApplyColorTheme();
		};
	}

	public static string ThemeOverride
	{
		get => Preferences.Default.Get(ThemeOverrideKey, ThemeSystem);
		set
		{
			Preferences.Default.Set(ThemeOverrideKey, value);
			if (Current is App app) app.ApplyColorTheme();
		}
	}

	/// <summary>Swaps in the theme-correct color dictionary (ColorsLight / ColorsDark / ColorsHighContrast,
	/// generated from Colors.Light.xaml / Colors.Dark.xaml / Colors.HighContrast.xaml — all three define the
	/// exact same keys, see their own comments) so every {DynamicResource X}/{StaticResource X} and
	/// Application.Current.Resources["X"] lookup across the app, including the ones GamePage's code-behind
	/// does per cell, is theme-correct without each page needing its own dark-mode logic. Called once at
	/// startup, again on every live OS theme change while ThemeOverride is "System" (MainActivity already
	/// declares ConfigChanges including UiMode, so this fires without an activity restart), and again
	/// whenever ThemeOverride's setter runs (SettingsPage's manual picker).
	///
	/// Instantiates the Colors* class rather than `new ResourceDictionary { Source = ... }`: MAUI only
	/// allows setting ResourceDictionary.Source from XAML, never from code, and throws
	/// InvalidOperationException("Source can only be set from XAML.") otherwise — crashed on every single
	/// launch until this was caught (2026-10-19), since this constructor runs unconditionally at startup.</summary>
	private void ApplyColorTheme()
	{
		bool isDark = ThemeOverride switch
		{
			ThemeLight => false,
			ThemeDark => true,
			ThemeHighContrast => true, // irrelevant once below picks ColorsHighContrast directly
			_ => RequestedTheme == AppTheme.Dark,
		};

		ResourceDictionary dictionary = ThemeOverride == ThemeHighContrast ? new ColorsHighContrast()
			: isDark ? new ColorsDark()
			: new ColorsLight();

		// Replace rather than accumulate: a dictionary from a previous call (or another theme) must not
		// linger merged in, or its keys would shadow/race with the new one's. MergedDictionaries is an
		// ICollection<ResourceDictionary> (no indexer/RemoveAt), so collect stale ones first, then Remove each.
		List<ResourceDictionary> stale = [.. Resources.MergedDictionaries.Where(d => d is ColorsLight or ColorsDark or ColorsHighContrast)];
		foreach (ResourceDictionary old in stale) Resources.MergedDictionaries.Remove(old);
		Resources.MergedDictionaries.Add(dictionary);
	}

	/// <summary>Startup terms/ads-disclosure gate (added 2026-11-12, see TermsGatePage's own header comment
	/// for the full behavior contract). This app has no SplashPage/Func&lt;Page&gt; handoff chain to extend
	/// (confirmed: none exists anywhere in this repo) -- CreateWindow just decides directly which page
	/// becomes the window's root, same as it always has, one `if` earlier than before.
	///
	/// ILicenseService is resolved via IPlatformApplication.Current rather than constructor injection:
	/// App itself is still constructed by the MAUI bootstrapper (via UseMauiApp&lt;App&gt;()) before
	/// CreateWindow runs, by which point the DI container backing IPlatformApplication.Current is already
	/// up (MauiProgram registers ILicenseService as a singleton) -- this is the documented fallback for
	/// exactly this situation when nothing more specific is available. If resolution somehow still returns
	/// null, fail OPEN straight to AppShell rather than risk crashing/blocking every single launch over a
	/// consent screen -- a missed gate on a never-observed DI failure is a much smaller problem than an
	/// unbootable app.</summary>
	protected override Window CreateWindow(IActivationState? activationState)
	{
		ILicenseService? license = IPlatformApplication.Current?.Services.GetService<ILicenseService>();
		bool needsGate = license != null && !AppPreferences.TermsAccepted && !license.State.IsPro;

		if (needsGate)
			return new Window(new TermsGatePage(license!, () => new AppShell()));

		return new Window(new AppShell());
	}
}
