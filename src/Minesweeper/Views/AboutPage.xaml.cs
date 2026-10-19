namespace Minesweeper.Views;

public partial class AboutPage : ContentPage
{
	public AboutPage()
	{
		InitializeComponent();
		VersionLabel.Text = $"Phiên bản {AppInfo.Current.VersionString} (bản dựng {AppInfo.Current.BuildString})";
	}

	private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
