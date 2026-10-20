using Minesweeper.Core;
using Minesweeper.Core.Licensing;
using Minesweeper.Services;

namespace Minesweeper.Views;

public partial class HomePage : ContentPage
{
	private readonly GameStatsStore _stats;
	private readonly SavedGameStore _saves;
	private readonly LevelProgressStore _levels;
	private readonly ILicenseService _license;

	public HomePage(GameStatsStore stats, SavedGameStore saves, LevelProgressStore levels, ILicenseService license)
	{
		InitializeComponent();
		_stats = stats;
		_saves = saves;
		_levels = levels;
		_license = license;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		ProBadge.IsVisible = !_license.State.IsPro;
		DailyDateLabel.Text = DateTime.Now.ToString("dddd, dd/MM/yyyy");
		EasyBestLabel.Text = BestTimeText(DifficultyLevel.Easy);
		MediumBestLabel.Text = BestTimeText(DifficultyLevel.Medium);
		HardBestLabel.Text = BestTimeText(DifficultyLevel.Hard);
		RefreshResumeCard();

		int unlocked = _levels.UnlockedLevel;
		int totalStars = 0;
		for (int n = 1; n <= unlocked; n++) totalStars += _levels.StarsFor(n);
		TutorialProgressLabel.Text = $"Đã mở màn {unlocked}/{LevelCatalog.MaxLevel} · {totalStars}★";
	}

	private void RefreshResumeCard()
	{
		SavedGame? save = _saves.Load();
		ResumeCard.IsVisible = save != null;
		if (save == null) return;

		string name = save.IsDaily ? "Thử thách hôm nay" : Difficulty.For((DifficultyLevel)save.DifficultyLevel).Name;
		ResumeDetailLabel.Text = $"{name} · {save.ElapsedSeconds / 60}:{save.ElapsedSeconds % 60:00}";
	}

	// Grid dimensions (9x9, 16x16, 30x16) are deliberately NOT shown here (owner's direction, 2026-10-20): the
	// 3 difficulty names already communicate "small/medium/large" well enough, and the actual board size now
	// adapts to the screen anyway (GamePage.BuildEmptyBoard), so quoting a fixed WxH would be misleading.
	private string BestTimeText(DifficultyLevel level)
	{
		int? best = _stats.BestTime(level);
		string size = $"{Difficulty.For(level).MineCount} mìn";
		return best is { } s ? $"{size} · Kỷ lục {s / 60:0}:{s % 60:00}" : size;
	}

	// Every navigation handler below goes through _nav (owner's report, 2026-10-20: no loading feedback meant
	// a slow device plus a real double/triple-tap fired GoToAsync more than once, pushing the same page onto
	// the Shell nav stack several times over, or crashing outright) -- see ActionGuard's own comment.
	private readonly ActionGuard _nav = new();

	private async void OnEasyClicked(object? sender, EventArgs e) => await GuardedAsync(sender, () => StartAsync(DifficultyLevel.Easy));
	private async void OnMediumClicked(object? sender, EventArgs e) => await GuardedAsync(sender, () => StartAsync(DifficultyLevel.Medium));
	private async void OnHardClicked(object? sender, EventArgs e) => await GuardedAsync(sender, () => StartAsync(DifficultyLevel.Hard));

	private async Task StartAsync(DifficultyLevel level)
	{
		await Shell.Current.GoToAsync(
			$"{nameof(GamePage)}?difficulty={level}&noGuess={NoGuessSwitch.IsToggled}&daily=False");
	}

	private async void OnDailyChallengeClicked(object? sender, EventArgs e) => await GuardedAsync(sender, () =>
		Shell.Current.GoToAsync($"{nameof(GamePage)}?difficulty={DifficultyLevel.Medium}&noGuess=True&daily=True"));

	private async void OnResumeClicked(object? sender, EventArgs e) => await GuardedAsync(sender, () =>
		Shell.Current.GoToAsync($"{nameof(GamePage)}?resume=True&difficulty={DifficultyLevel.Easy}&noGuess=False&daily=False"));

	private async void OnTutorialTapped(object? sender, EventArgs e) => await GuardedAsync(sender, () => Shell.Current.GoToAsync(nameof(LevelsPage)));
	private async void OnDailyCalendarClicked(object? sender, EventArgs e) => await GuardedAsync(sender, () => Shell.Current.GoToAsync(nameof(DailyCalendarPage)));

	private async void OnSettingsClicked(object? sender, EventArgs e) => await GuardedAsync(sender, () => Shell.Current.GoToAsync(nameof(SettingsPage)));
	private async void OnAboutClicked(object? sender, EventArgs e) => await GuardedAsync(sender, () => Shell.Current.GoToAsync(nameof(AboutPage)));
	private async void OnProBadgeClicked(object? sender, EventArgs e) => await GuardedAsync(sender, () => Shell.Current.GoToAsync(nameof(SettingsPage)));

	private async Task GuardedAsync(object? sender, Func<Task> action)
	{
		if (!_nav.TryEnter(sender as View)) return;
		try { await action(); }
		finally { _nav.Exit(sender as View); }
	}
}
