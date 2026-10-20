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

	public DailyCalendarPage(DailyProgressStore progress)
	{
		InitializeComponent();
		_progress = progress;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
		LoadMonth(new DateOnly(today.Year, today.Month, 1));
	}

	private void LoadMonth(DateOnly monthStart)
	{
		_currentMonth = monthStart;
		MonthLabel.Text = $"{MonthNames[_currentMonth.Month - 1]}, {_currentMonth.Year}";

		int daysInMonth = DateTime.DaysInMonth(_currentMonth.Year, _currentMonth.Month);
		int firstWeekday = ((int)_currentMonth.DayOfWeek + 6) % 7; // DayOfWeek has Sunday=0; this shifts to Monday=0 to match the T2..CN header

		var items = new List<CalendarDayItem>(firstWeekday + daysInMonth);
		for (int i = 0; i < firstWeekday; i++)
			items.Add(new CalendarDayItem { Day = 0, IsToday = false, IsCompleted = false, IsMissed = false });

		DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
		DateOnly firstTracked = _progress.FirstTrackedDate;
		for (int d = 1; d <= daysInMonth; d++)
		{
			var date = new DateOnly(_currentMonth.Year, _currentMonth.Month, d);
			bool isCompleted = _progress.IsCompleted(date);
			bool isMissed = date < today && date >= firstTracked && !isCompleted;
			items.Add(new CalendarDayItem { Day = d, IsToday = date == today, IsCompleted = isCompleted, IsMissed = isMissed });
		}

		DaysCollection.ItemsSource = items;

		DateOnly thisMonthStart = new(today.Year, today.Month, 1);
		NextMonthButton.IsEnabled = _currentMonth < thisMonthStart;
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
