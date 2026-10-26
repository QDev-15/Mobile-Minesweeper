using System.Globalization;
using Minesweeper.Resources.Strings;
using Minesweeper.Services;

namespace Minesweeper.Views;

public partial class PrivacyPolicyPage : ContentPage
{
	private static readonly DateOnly LastUpdated = new(2026, 10, 19);
	private readonly ActionGuard _nav = new();

	public PrivacyPolicyPage()
	{
		InitializeComponent();
		string date = LastUpdated.ToDateTime(TimeOnly.MinValue).ToString("d", CultureInfo.CurrentUICulture);
		UpdatedLabel.Text = AppStrings.Privacy_LastUpdated(date);
	}

	private async void OnBackClicked(object? sender, EventArgs e)
	{
		if (!_nav.TryEnter(sender as View)) return;
		try { await Shell.Current.GoToAsync(".."); }
		finally { _nav.Exit(sender as View); }
	}
}
