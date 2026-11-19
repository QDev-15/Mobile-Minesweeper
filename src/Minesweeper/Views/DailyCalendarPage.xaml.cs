using System.Globalization;
using Minesweeper.Core;
using Minesweeper.Resources.Strings;
using Minesweeper.Services;

namespace Minesweeper.Views;

public partial class DailyCalendarPage : ContentPage
{
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

		// Monday-first order to match the grid below (same Sunday=0 -> Monday=0 shift as LoadMonth's
		// firstWeekday calculation) -- locale day-name DATA, not translatable app copy, so this reads from
		// CultureInfo instead of a resx key.
		var dayNames = CultureInfo.CurrentUICulture.DateTimeFormat.AbbreviatedDayNames;
		Label[] weekdayLabels = [Weekday0Label, Weekday1Label, Weekday2Label, Weekday3Label, Weekday4Label, Weekday5Label, Weekday6Label];
		for (int i = 0; i < 7; i++)
			weekdayLabels[i].Text = dayNames[(i + 1) % 7];
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		DateOnly today = DateOnly.FromDateTime(DateTime.Now);
		_selectedDate = today;
		LoadMonth(new DateOnly(today.Year, today.Month, 1));
	}

	private void LoadMonth(DateOnly monthStart)
	{
		_currentMonth = monthStart;
		string monthName = CultureInfo.CurrentUICulture.DateTimeFormat.GetMonthName(_currentMonth.Month);
		MonthLabel.Text = $"{monthName}, {_currentMonth.Year}";

		int daysInMonth = DateTime.DaysInMonth(_currentMonth.Year, _currentMonth.Month);
		int firstWeekday = ((int)_currentMonth.DayOfWeek + 6) % 7; // DayOfWeek has Sunday=0; this shifts to Monday=0 to match the T2..CN header

		// Local date, not UTC: the player's own midnight is the day boundary that should open a new Daily
		// Challenge, not UTC midnight (bug report, 2026-11-18 -- UTC+7 players saw the new day open around
		// 7am local instead of right after midnight).
		DateOnly today = DateOnly.FromDateTime(DateTime.Now);

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
				IsSelectable = date <= today, IsSelected = date == _selectedDate, BestTimeSeconds = _progress.BestTime(date),
			});
		}

		DaysCollection.ItemsSource = items;
		PlayNowButton.Text = _selectedDate == today ? AppStrings.Common_PlayNow : AppStrings.Common_PlayOnDate(_selectedDate.ToString("dd/MM"));

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
