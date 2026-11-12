using Minesweeper.Core.Licensing;
using Minesweeper.Resources.Strings;
using Minesweeper.Services;

namespace Minesweeper.Views;

/// <summary>Blocking startup consent gate (added 2026-11-12, same behavior contract as the equivalent
/// already-shipped feature in the sibling PdfReader/DocScanner/LogoExtract repos, ported to this app's own
/// idioms -- no splash/Func&lt;Page&gt; chain to extend here since this app has no SplashPage at all, see
/// App.CreateWindow's own comment). Constructed directly as <c>Window.Page</c> by <see cref="App"/>, before
/// <see cref="AppShell"/> exists, so it takes <c>createShell</c>-the-delegate rather than
/// navigating anywhere -- the same "replace Window.Page" swap technique <c>AppLanguage.SetOverride</c>
/// already uses elsewhere in this app.
///
/// Three ways out, matching the design brief exactly:
/// 1. Buy Pro (Purchased/AlreadyOwned) -&gt; proceed, <see cref="AppPreferences.TermsAccepted"/> is
///    deliberately left false so a later refund correctly re-shows this gate instead of skipping it
///    forever on a stale flag.
/// 2. Tick the checkbox + "Đồng ý &amp; Tiếp tục" -&gt; persist TermsAccepted, proceed with ads as normal.
/// 3. "Từ chối &amp; Thoát", or hardware/gesture Back -&gt; exit the whole app via FinishAffinity (not just
///    pop a nav stack -- there is nothing to pop back to; this page IS the window's root).</summary>
public partial class TermsGatePage : ContentPage
{
	private readonly ILicenseService _license;
	private readonly Func<Page> _createShell;
	private readonly ActionGuard _nav = new();

	// Guards against ProceedToShell running twice (e.g. the background license refresh resolves Pro=true
	// a moment after the player has already tapped "Đồng ý & Tiếp tục") -- a second Window.Page swap would
	// be harmless but pointless, this just makes the intent explicit.
	private bool _proceeded;

	public TermsGatePage(ILicenseService license, Func<Page> createShell)
	{
		InitializeComponent();
		_license = license;
		_createShell = createShell;

		BuyProButton.Text = _license.ProPriceText is { Length: > 0 } price
			? $"{AppStrings.TermsGate_BuyPro} · {price}"
			: AppStrings.TermsGate_BuyPro;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		// Fire-and-forget on purpose (design brief: "kick off, don't block the page's own content on") --
		// covers a Pro purchase already completed on another device, without making this page wait on
		// network before the player can even read it.
		_ = RefreshLicenseAndMaybeProceedAsync();
	}

	// Short timeout (~2.5s), unlike OnBuyClicked/OnAgreeClicked below which deliberately have none (matching
	// this app's own existing SettingsPage.OnBuyClicked precedent -- no shared timeout helper to reuse here):
	// this refresh runs unprompted on every single launch, not from a user tap, so it must not leave the
	// gate looking hung if Google Play is slow/unreachable. A user-initiated purchase/restore tap, by
	// contrast, is explicitly something the player chose to wait for.
	private async Task RefreshLicenseAndMaybeProceedAsync()
	{
		try { await Task.WhenAny(_license.RefreshAsync(), Task.Delay(TimeSpan.FromSeconds(2.5))); }
		catch { /* best-effort; Buy/Agree below still work even if this background refresh failed */ }

		if (_license.State.IsPro) ProceedToShell(); // Pro already owned elsewhere -- no flag set, just proceed.
	}

	private void ProceedToShell()
	{
		if (_proceeded) return;
		_proceeded = true;
		if (Application.Current is { } app && app.Windows.Count > 0)
			app.Windows[0].Page = _createShell();
	}

	private void OnAgreeRowTapped(object? sender, EventArgs e) => AgreeCheckBox.IsChecked = !AgreeCheckBox.IsChecked;

	private void OnAgreeCheckBoxChanged(object? sender, CheckedChangedEventArgs e)
	{
		AgreeButton.IsEnabled = e.Value;
		AgreeButton.Opacity = e.Value ? 1 : 0.45;
	}

	private async void OnBuyClicked(object? sender, EventArgs e)
	{
		// Same _nav + Busy shape as SettingsPage.OnBuyClicked (see that method's own comment): Busy alone
		// doesn't block input, so a slow network plus an impatient second tap could fire a second
		// PurchaseProAsync while the first is still in flight.
		if (!_nav.TryEnter(sender as View)) return;
		try
		{
			Busy.IsVisible = true;
			Busy.IsRunning = true;
			PurchaseOutcome outcome = await _license.PurchaseProAsync();
			Busy.IsRunning = false;
			Busy.IsVisible = false;

			if (outcome is PurchaseOutcome.Purchased or PurchaseOutcome.AlreadyOwned)
			{
				ProceedToShell();
				return;
			}
			if (outcome == PurchaseOutcome.Error)
				await DisplayAlertAsync(AppStrings.Settings_Error, _license.LastError ?? AppStrings.Settings_PurchaseFailed, "OK");
			// Cancelled: no-op, the player stays on the gate.
		}
		finally { _nav.Exit(sender as View); }
	}

	private void OnAgreeClicked(object? sender, EventArgs e)
	{
		if (!AgreeCheckBox.IsChecked) return; // belt-and-suspenders -- the button is already disabled until checked.
		AppPreferences.TermsAccepted = true;
		ProceedToShell();
	}

	private void OnDeclineClicked(object? sender, EventArgs e) => ExitApp();

	// Synchronous and final on purpose, same reasoning GamePage's own OnBackButtonPressed comment gives for
	// why this can't await a confirm dialog -- except here there is nothing to confirm either way: both the
	// hardware/gesture back button and the explicit decline button must always exit, immediately.
	protected override bool OnBackButtonPressed()
	{
		ExitApp();
		return true;
	}

	private static void ExitApp() => Platform.CurrentActivity?.FinishAffinity();
}
