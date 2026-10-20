using Minesweeper.Services;

namespace Minesweeper.Views;

public partial class PrivacyPolicyPage : ContentPage
{
	private readonly ActionGuard _nav = new();

	public PrivacyPolicyPage()
	{
		InitializeComponent();
		UpdatedLabel.Text = "Cập nhật lần cuối: 19/10/2026";
	}

	private async void OnBackClicked(object? sender, EventArgs e)
	{
		if (!_nav.TryEnter(sender as View)) return;
		try { await Shell.Current.GoToAsync(".."); }
		finally { _nav.Exit(sender as View); }
	}
}
