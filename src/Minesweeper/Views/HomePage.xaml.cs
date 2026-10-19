using Minesweeper.Core;
using Minesweeper.Services;

namespace Minesweeper.Views;

public partial class HomePage : ContentPage
{
	private readonly GameStatsStore _stats;
	private readonly SavedGameStore _saves;
	private readonly LevelProgressStore _levels;

	public HomePage(GameStatsStore stats, SavedGameStore saves, LevelProgressStore levels)
	{
		InitializeComponent();
		_stats = stats;
		_saves = saves;
		_levels = levels;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
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
		ResumeDetailLabel.Text = $"{name} · {save.Width}x{save.Height} · {save.ElapsedSeconds / 60}:{save.ElapsedSeconds % 60:00}";
	}

	private string BestTimeText(DifficultyLevel level)
	{
		int? best = _stats.BestTime(level);
		Difficulty d = Difficulty.For(level);
		string size = $"{d.Width}x{d.Height}, {d.MineCount} mìn";
		return best is { } s ? $"{size} · Kỷ lục {s / 60:0}:{s % 60:00}" : size;
	}

	private async void OnEasyClicked(object? sender, EventArgs e) => await StartAsync(DifficultyLevel.Easy);
	private async void OnMediumClicked(object? sender, EventArgs e) => await StartAsync(DifficultyLevel.Medium);
	private async void OnHardClicked(object? sender, EventArgs e) => await StartAsync(DifficultyLevel.Hard);

	private async Task StartAsync(DifficultyLevel level)
	{
		await Shell.Current.GoToAsync(
			$"{nameof(GamePage)}?difficulty={level}&noGuess={NoGuessSwitch.IsToggled}&daily=False");
	}

	private async void OnDailyChallengeClicked(object? sender, EventArgs e)
	{
		await Shell.Current.GoToAsync(
			$"{nameof(GamePage)}?difficulty={DifficultyLevel.Medium}&noGuess=True&daily=True");
	}

	private async void OnResumeClicked(object? sender, EventArgs e)
	{
		await Shell.Current.GoToAsync($"{nameof(GamePage)}?resume=True&difficulty={DifficultyLevel.Easy}&noGuess=False&daily=False");
	}

	private async void OnTutorialTapped(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(LevelsPage));

	private async void OnSettingsClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(SettingsPage));
	private async void OnAboutClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(AboutPage));
}
