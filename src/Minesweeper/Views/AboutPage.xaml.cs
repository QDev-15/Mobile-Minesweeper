using Minesweeper.Services;

namespace Minesweeper.Views;

public partial class AboutPage : ContentPage
{
	private readonly ActionGuard _nav = new();

	public AboutPage()
	{
		InitializeComponent();
		VersionLabel.Text = $"Phiên bản {AppInfo.Current.VersionString} (bản dựng {AppInfo.Current.BuildString})";
	}

	private async void OnBackClicked(object? sender, EventArgs e)
	{
		if (!_nav.TryEnter(sender as View)) return;
		try { await Shell.Current.GoToAsync(".."); }
		finally { _nav.Exit(sender as View); }
	}
}
