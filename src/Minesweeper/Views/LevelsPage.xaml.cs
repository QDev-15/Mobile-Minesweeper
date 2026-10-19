using Minesweeper.Core;
using Minesweeper.Services;

namespace Minesweeper.Views;

public partial class LevelsPage : ContentPage
{
	private readonly LevelProgressStore _levels;

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
		for (int n = LevelCatalog.MinLevel; n <= LevelCatalog.MaxLevel; n++)
			items.Add(new LevelItem { Number = n, Unlocked = n <= unlocked, Stars = _levels.StarsFor(n), IsNext = n == unlocked });
		LevelsCollection.ItemsSource = items;
	}

	private async void OnLevelTapped(object? sender, EventArgs e)
	{
		if (sender is not BindableObject { BindingContext: LevelItem item } || !item.Unlocked) return;
		await Shell.Current.GoToAsync($"{nameof(GamePage)}?level={item.Number}");
	}

	private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
