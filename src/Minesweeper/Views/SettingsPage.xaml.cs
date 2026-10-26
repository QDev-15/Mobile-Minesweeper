using Minesweeper.Core.Licensing;
using Minesweeper.Resources.Strings;
using Minesweeper.Services;

namespace Minesweeper.Views;

public partial class SettingsPage : ContentPage
{
	private readonly ILicenseService _license;
	private readonly ActionGuard _nav = new();
	private bool _suppressLanguagePickerEvent;

	public SettingsPage(ILicenseService license)
	{
		InitializeComponent();
		_license = license;
		VersionCaptionLabel.Text = AppStrings.Common_Version(AppInfo.Current.VersionString, AppInfo.Current.BuildString);

		LanguagePicker.ItemsSource = AppLanguage.Supported.Select(l => l.NativeName).ToList();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		Refresh();
		RefreshThemeButtons();
		SoundSwitch.IsToggled = AppPreferences.SoundEnabled;
		VibrationSwitch.IsToggled = AppPreferences.VibrationEnabled;
		RefreshLanguagePicker();
		_ = _license.RefreshAsync();
	}

	private void Refresh() => LicenseLabel.Text = _license.State.IsPro ? AppStrings.License_Pro : AppStrings.License_Free;

	// ===== Language picker =====

	private void RefreshLanguagePicker()
	{
		int index = Array.FindIndex(AppLanguage.Supported, l => l.Code == AppLanguage.CurrentCode);
		_suppressLanguagePickerEvent = true;
		LanguagePicker.SelectedIndex = index;
		_suppressLanguagePickerEvent = false;
	}

	// Rebuilds AppShell (AppLanguage.SetOverride) so every {x:Static}-bound label across the app re-resolves
	// in the new language -- this page itself is replaced as part of that, so nothing further to refresh here.
	private void OnLanguagePickerChanged(object? sender, EventArgs e)
	{
		if (_suppressLanguagePickerEvent) return;
		int index = LanguagePicker.SelectedIndex;
		if (index < 0 || index >= AppLanguage.Supported.Length) return;
		string code = AppLanguage.Supported[index].Code;
		if (code == AppLanguage.CurrentCode) return;
		AppLanguage.SetOverride(code);
	}

	private void OnSoundToggled(object? sender, ToggledEventArgs e) => AppPreferences.SoundEnabled = e.Value;
	private void OnVibrationToggled(object? sender, ToggledEventArgs e) => AppPreferences.VibrationEnabled = e.Value;

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
		// _nav, not just Busy: Busy is a spinner the player can still see and tap straight through (it does
		// not block input), so without this a slow network plus an impatient second tap could fire a SECOND
		// PurchaseProAsync while the first is still in flight (owner's report, 2026-10-20).
		if (!_nav.TryEnter(sender as View)) return;
		try
		{
			Busy.IsVisible = true;
			Busy.IsRunning = true;
			PurchaseOutcome outcome = await _license.PurchaseProAsync();
			Busy.IsRunning = false;
			Busy.IsVisible = false;
			Refresh();

			if (outcome == PurchaseOutcome.Error)
				await DisplayAlertAsync(AppStrings.Settings_Error, _license.LastError ?? AppStrings.Settings_PurchaseFailed, "OK");
		}
		finally { _nav.Exit(sender as View); }
	}

	private async void OnRestoreClicked(object? sender, EventArgs e)
	{
		if (!_nav.TryEnter(sender as View)) return;
		try
		{
			Busy.IsVisible = true;
			Busy.IsRunning = true;
			PurchaseOutcome outcome = await _license.RestoreAsync();
			Busy.IsRunning = false;
			Busy.IsVisible = false;
			Refresh();

			string message = outcome switch
			{
				PurchaseOutcome.AlreadyOwned or PurchaseOutcome.Purchased => AppStrings.Settings_RestoreSuccess,
				PurchaseOutcome.Error => _license.LastError ?? AppStrings.Settings_RestoreFailed,
				_ => AppStrings.Settings_RestoreNotFound,
			};
			await DisplayAlertAsync(AppStrings.Settings_RestoreTitle, message, "OK");
		}
		finally { _nav.Exit(sender as View); }
	}

	private async void OnAboutTapped(object? sender, EventArgs e)
	{
		if (!_nav.TryEnter(sender as View)) return;
		try { await Shell.Current.GoToAsync(nameof(AboutPage)); }
		finally { _nav.Exit(sender as View); }
	}

	private async void OnPrivacyPolicyTapped(object? sender, EventArgs e)
	{
		if (!_nav.TryEnter(sender as View)) return;
		try { await Shell.Current.GoToAsync(nameof(PrivacyPolicyPage)); }
		finally { _nav.Exit(sender as View); }
	}

	private async void OnBackClicked(object? sender, EventArgs e)
	{
		if (!_nav.TryEnter(sender as View)) return;
		try { await Shell.Current.GoToAsync(".."); }
		finally { _nav.Exit(sender as View); }
	}
}
