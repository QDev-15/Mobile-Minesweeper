using Minesweeper.Core.Licensing;

namespace Minesweeper.Views;

public partial class SettingsPage : ContentPage
{
	private readonly ILicenseService _license;

	public SettingsPage(ILicenseService license)
	{
		InitializeComponent();
		_license = license;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		Refresh();
		_ = _license.RefreshAsync();
	}

	private void Refresh() => LicenseLabel.Text = _license.State.SummaryText;

	private async void OnBuyClicked(object? sender, EventArgs e)
	{
		Busy.IsVisible = true;
		Busy.IsRunning = true;
		PurchaseOutcome outcome = await _license.PurchaseProAsync();
		Busy.IsRunning = false;
		Busy.IsVisible = false;
		Refresh();

		if (outcome == PurchaseOutcome.Error)
			await DisplayAlertAsync("Lỗi", _license.LastError ?? "Không thể mua lúc này.", "OK");
	}

	private async void OnRestoreClicked(object? sender, EventArgs e)
	{
		Busy.IsVisible = true;
		Busy.IsRunning = true;
		PurchaseOutcome outcome = await _license.RestoreAsync();
		Busy.IsRunning = false;
		Busy.IsVisible = false;
		Refresh();

		string message = outcome switch
		{
			PurchaseOutcome.AlreadyOwned or PurchaseOutcome.Purchased => "Đã khôi phục gói Pro.",
			PurchaseOutcome.Error => _license.LastError ?? "Không khôi phục được.",
			_ => "Không tìm thấy giao dịch Pro nào trước đó.",
		};
		await DisplayAlertAsync("Khôi phục", message, "OK");
	}

	private async void OnAboutTapped(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(AboutPage));
	private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
