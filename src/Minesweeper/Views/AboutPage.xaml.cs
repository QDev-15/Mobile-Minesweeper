using Minesweeper.Resources.Strings;
using Minesweeper.Services;

namespace Minesweeper.Views;

public partial class AboutPage : ContentPage
{
	private readonly ActionGuard _nav = new();

	public AboutPage()
	{
		InitializeComponent();
		// Same resx key/format as SettingsPage's own version caption (the two used different punctuation for
		// identical content before this -- consolidated to one key, 2026-10-26, so a translator only has to
		// get this phrase right once).
		VersionLabel.Text = AppStrings.Common_Version(AppInfo.Current.VersionString, AppInfo.Current.BuildString);
	}

	private async void OnBackClicked(object? sender, EventArgs e)
	{
		if (!_nav.TryEnter(sender as View)) return;
		try { await Shell.Current.GoToAsync(".."); }
		finally { _nav.Exit(sender as View); }
	}
}
