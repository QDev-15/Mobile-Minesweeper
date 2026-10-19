namespace Minesweeper.Views;

public partial class PrivacyPolicyPage : ContentPage
{
	public PrivacyPolicyPage()
	{
		InitializeComponent();
		UpdatedLabel.Text = "Cập nhật lần cuối: 19/10/2026";
	}

	private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
