namespace Minesweeper;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
		ApplyColorTheme(RequestedTheme);
		RequestedThemeChanged += (_, e) => ApplyColorTheme(e.RequestedTheme);
	}

	/// <summary>Swaps in the theme-correct color dictionary (Colors.Light.xaml / Colors.Dark.xaml -- both
	/// define the exact same keys, see their own comments) so every {StaticResource X} and
	/// Application.Current.Resources["X"] lookup across the app, including the ones GamePage's code-behind
	/// does per cell, is theme-correct without each page needing its own dark-mode logic. Called once at
	/// startup and again on every live theme change (the device's light/dark setting, or Android's own
	/// battery-saver-triggered dark mode) -- MainActivity already declares ConfigChanges including UiMode,
	/// so this fires without an activity restart.</summary>
	private void ApplyColorTheme(AppTheme theme)
	{
		string file = theme == AppTheme.Dark ? "Colors.Dark.xaml" : "Colors.Light.xaml";
		var dictionary = new ResourceDictionary { Source = new Uri($"Resources/Styles/{file}", UriKind.Relative) };

		// Replace rather than accumulate: a dictionary from a previous call (or the other theme) must not
		// linger merged in, or its keys would shadow/race with the new one's. MergedDictionaries is an
		// ICollection<ResourceDictionary> (no indexer/RemoveAt), so collect stale ones first, then Remove each.
		List<ResourceDictionary> stale = [.. Resources.MergedDictionaries.Where(d => d.Source?.OriginalString.Contains("Colors.") == true)];
		foreach (ResourceDictionary old in stale) Resources.MergedDictionaries.Remove(old);
		Resources.MergedDictionaries.Add(dictionary);
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}
