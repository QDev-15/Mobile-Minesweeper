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

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}
