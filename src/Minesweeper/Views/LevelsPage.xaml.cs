using Minesweeper.Core;
using Minesweeper.Services;

namespace Minesweeper.Views;

public partial class LevelsPage : ContentPage
{
	private readonly LevelProgressStore _levels;
	private readonly ActionGuard _nav = new();

	public LevelsPage(LevelProgressStore levels)
	{
		InitializeComponent();
		_levels = levels;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		int unlocked = _levels.UnlockedLevel;
		var items = new List<LevelItem>(LevelCatalog.MaxLevel);
		int totalStars = 0;
		for (int n = LevelCatalog.MinLevel; n <= LevelCatalog.MaxLevel; n++)
		{
			int stars = _levels.StarsFor(n);
			totalStars += stars;
			items.Add(new LevelItem { Number = n, Unlocked = n <= unlocked, Stars = stars, IsNext = n == unlocked });
		}
		LevelsCollection.ItemsSource = items;
		StatsLabel.Text = $"Đã mở {unlocked}/{LevelCatalog.MaxLevel} · {totalStars}★";
	}

	private async void OnLevelTapped(object? sender, EventArgs e)
	{
		if (sender is not BindableObject { BindingContext: LevelItem item } || !item.Unlocked) return;
		if (!_nav.TryEnter(sender as View)) return;
		try { await Shell.Current.GoToAsync($"{nameof(GamePage)}?level={item.Number}"); }
		finally { _nav.Exit(sender as View); }
	}

	private async void OnBackClicked(object? sender, EventArgs e)
	{
		if (!_nav.TryEnter(sender as View)) return;
		try { await Shell.Current.GoToAsync(".."); }
		finally { _nav.Exit(sender as View); }
	}
}
