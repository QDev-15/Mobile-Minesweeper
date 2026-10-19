using Minesweeper.Core.Licensing;

namespace Minesweeper.Views;

public partial class SettingsPage : ContentPage
{
	private readonly ILicenseService _license;

	public SettingsPage(ILicenseService license)
	{
		InitializeComponent();
		_license = license;
		VersionCaptionLabel.Text = $"Phiên bản {AppInfo.Current.VersionString} · Bản dựng {AppInfo.Current.BuildString}";
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		Refresh();
		RefreshThemeButtons();
		_ = _license.RefreshAsync();
	}

	private void Refresh() => LicenseLabel.Text = _license.State.SummaryText;

	// ===== Theme picker (design spec §1.4/§3.6) =====

	private void RefreshThemeButtons()
	{
		SetThemeButtonState(ThemeSystemButton, App.ThemeOverride == App.ThemeSystem);
		SetThemeButtonState(ThemeLightButton, App.ThemeOverride == App.ThemeLight);
		SetThemeButtonState(ThemeDarkButton, App.ThemeOverride == App.ThemeDark);
		SetThemeButtonState(ThemeHighContrastButton, App.ThemeOverride == App.ThemeHighContrast);
	}

	private void SetThemeButtonState(Button button, bool selected)
	{
		var primary = (Color)Application.Current!.Resources["Primary"];
		var primarySoft = (Color)Application.Current.Resources["PrimarySoft"];
		var milk = (Color)Application.Current.Resources["MilkBackground"];
		var offBlack = (Color)Application.Current.Resources["OffBlack"];
		button.BackgroundColor = selected ? primarySoft : milk;
		button.TextColor = selected ? primary : offBlack;
		button.BorderColor = primary;
		button.BorderWidth = selected ? 1.5 : 0;
	}

	private void SetTheme(string theme)
	{
		App.ThemeOverride = theme;
		RefreshThemeButtons();
	}

	private void OnThemeSystemClicked(object? sender, EventArgs e) => SetTheme(App.ThemeSystem);
	private void OnThemeLightClicked(object? sender, EventArgs e) => SetTheme(App.ThemeLight);
	private void OnThemeDarkClicked(object? sender, EventArgs e) => SetTheme(App.ThemeDark);
	private void OnThemeHighContrastClicked(object? sender, EventArgs e) => SetTheme(App.ThemeHighContrast);

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
	private async void OnPrivacyPolicyTapped(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(PrivacyPolicyPage));
	private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
