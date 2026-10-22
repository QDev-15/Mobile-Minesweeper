using Minesweeper.Core;
using Minesweeper.Services;

namespace Minesweeper.Views;

public partial class DailyCalendarPage : ContentPage
{
	private static readonly string[] MonthNames =
	[
		"Tháng 1", "Tháng 2", "Tháng 3", "Tháng 4", "Tháng 5", "Tháng 6",
		"Tháng 7", "Tháng 8", "Tháng 9", "Tháng 10", "Tháng 11", "Tháng 12",
	];

	private readonly DailyProgressStore _progress;
	private readonly ActionGuard _nav = new();
	private DateOnly _currentMonth;
	// Which day "Chơi ngay" targets -- starts on today (so a player who just wants today's puzzle can still
	// tap straight through), but a day tap below only MOVES this selection, it no longer navigates on its
	// own (owner's direction, 2026-10-20: picking a day and playing it are now two separate steps).
	private DateOnly _selectedDate;

	public DailyCalendarPage(DailyProgressStore progress)
	{
		InitializeComponent();
		_progress = progress;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
		_selectedDate = today;
		LoadMonth(new DateOnly(today.Year, today.Month, 1));
	}

	private void LoadMonth(DateOnly monthStart)
	{
		_currentMonth = monthStart;
		MonthLabel.Text = $"{MonthNames[_currentMonth.Month - 1]}, {_currentMonth.Year}";

		int daysInMonth = DateTime.DaysInMonth(_currentMonth.Year, _currentMonth.Month);
		int firstWeekday = ((int)_currentMonth.DayOfWeek + 6) % 7; // DayOfWeek has Sunday=0; this shifts to Monday=0 to match the T2..CN header

		DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

		var items = new List<CalendarDayItem>(firstWeekday + daysInMonth);
		for (int i = 0; i < firstWeekday; i++)
			items.Add(new CalendarDayItem { Day = 0, Date = today, IsToday = false, IsCompleted = false, IsMissed = false, IsSelectable = false, IsSelected = false });

		DateOnly firstTracked = _progress.FirstTrackedDate;
		for (int d = 1; d <= daysInMonth; d++)
		{
			var date = new DateOnly(_currentMonth.Year, _currentMonth.Month, d);
			bool isCompleted = _progress.IsCompleted(date);
			bool isMissed = date < today && date >= firstTracked && !isCompleted;
			items.Add(new CalendarDayItem
			{
				Day = d, Date = date, IsToday = date == today, IsCompleted = isCompleted, IsMissed = isMissed,
				IsSelectable = date <= today, IsSelected = date == _selectedDate,
			});
		}

		DaysCollection.ItemsSource = items;
		PlayNowButton.Text = _selectedDate == today ? "Chơi ngay" : $"Chơi ngày {_selectedDate:dd/MM}";

		DateOnly thisMonthStart = new(today.Year, today.Month, 1);
		NextMonthButton.IsEnabled = _currentMonth < thisMonthStart;
	}

	// Only moves the selection (re-rendering the month so IsSelected highlights the new day) -- PlayNowButton
	// below is the one and only thing that actually navigates into a puzzle now.
	private void OnDayTapped(object? sender, TappedEventArgs e)
	{
		if (e.Parameter is not CalendarDayItem { IsSelectable: true } item || item.Date == _selectedDate) return;
		_selectedDate = item.Date;
		LoadMonth(_currentMonth);
	}

	// Opens a fresh Daily Challenge puzzle for the selected day -- GamePage's dailyDate param (default "today"
	// when absent) is only used to decide which calendar date a win gets recorded against
	// (GamePage.HandleGameEndIfNeeded), not the board's content (2026-10-21: Daily generates a new random
	// board per play now, same as every other mode, not one fixed board per date).
	private async void OnPlayNowClicked(object? sender, EventArgs e)
	{
		if (!_nav.TryEnter(sender as View)) return;
		try
		{
			await Shell.Current.GoToAsync(
				$"{nameof(GamePage)}?difficulty={DifficultyLevel.Medium}&daily=True&dailyDate={_selectedDate:yyyy-MM-dd}");
		}
		finally { _nav.Exit(sender as View); }
	}

	private void OnPrevMonthClicked(object? sender, EventArgs e) => LoadMonth(_currentMonth.AddMonths(-1));

	private void OnNextMonthClicked(object? sender, EventArgs e)
	{
		if (!NextMonthButton.IsEnabled) return;
		LoadMonth(_currentMonth.AddMonths(1));
	}

	private async void OnBackClicked(object? sender, EventArgs e)
	{
		if (!_nav.TryEnter(sender as View)) return;
		try { await Shell.Current.GoToAsync(".."); }
		finally { _nav.Exit(sender as View); }
	}
}
