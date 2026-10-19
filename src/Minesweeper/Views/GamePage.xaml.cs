using CommunityToolkit.Maui.Behaviors;
using Minesweeper.Core;
using Minesweeper.Services;

namespace Minesweeper.Views;

[QueryProperty(nameof(DifficultyParam), "difficulty")]
[QueryProperty(nameof(NoGuessParam), "noGuess")]
[QueryProperty(nameof(DailyParam), "daily")]
[QueryProperty(nameof(ResumeParam), "resume")]
[QueryProperty(nameof(LevelParam), "level")]
public partial class GamePage : ContentPage
{
	private readonly GameStatsStore _stats;
	private readonly IAdsService _ads;
	private readonly SavedGameStore _saves;
	private readonly LevelProgressStore _levels;

	private DifficultyLevel _level = DifficultyLevel.Easy;
	private bool _noGuess;
	private bool _isDaily;
	private bool _resume;
	private int _levelNumber; // 0 = not Tutorial mode; 1-100 = LevelCatalog level
	private Difficulty _difficulty = Difficulty.Easy;

	private GameEngine? _engine;
	private Border[,]? _cellBorders;
	private Label[,]? _cellLabels;
	// What RefreshAllCells last painted each cell as; compared against engine.StateAt on the next call so only
	// a cell that is NEWLY Revealed/Flagged this pass gets the pop animation below, not one that was already
	// sitting open/flagged from an earlier call (e.g. the periodic timer tick never touches this, but a second
	// Reveal elsewhere on the board re-runs the whole grid and must not replay the pop on untouched cells).
	private CellState?[,]? _priorCellState;
	private bool _boardStarted; // true once the first reveal has happened (mines are placed)
	private bool _flagMode;
	private bool _loaded;

	private IDispatcherTimer? _timer;
	private int _elapsedSeconds;

	// Shared instance: a Shadow is a reference type and MAUI is fine with the same one assigned to many
	// Border.Shadow properties, so this avoids one allocation per cell per refresh on a 480-cell Hard board.
	private static readonly Shadow HiddenShadow = new() { Brush = Colors.Black, Opacity = 0.28f, Radius = 3, Offset = new Point(0, 1.5) };

	public string DifficultyParam
	{
		set => Enum.TryParse(Uri.UnescapeDataString(value), out _level);
	}

	public string NoGuessParam
	{
		set => bool.TryParse(Uri.UnescapeDataString(value), out _noGuess);
	}

	public string DailyParam
	{
		set => bool.TryParse(Uri.UnescapeDataString(value), out _isDaily);
	}

	public string ResumeParam
	{
		set => bool.TryParse(Uri.UnescapeDataString(value), out _resume);
	}

	public string LevelParam
	{
		set => int.TryParse(Uri.UnescapeDataString(value), out _levelNumber);
	}

	public GamePage(GameStatsStore stats, IAdsService ads, SavedGameStore saves, LevelProgressStore levels)
	{
		InitializeComponent();
		_stats = stats;
		_ads = ads;
		_saves = saves;
		_levels = levels;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		if (_loaded) return;
		_loaded = true;

		if (_resume && _saves.Load() is { } save)
		{
			ResumeFrom(save);
			return;
		}

		if (_levelNumber > 0)
		{
			_difficulty = LevelCatalog.DifficultyFor(_levelNumber);
			_isDaily = false;
			_noGuess = true; // Tutorial levels are always No-guess (plan section 5: a tutorial should teach logic, never force a guess)
		}
		else
		{
			_difficulty = Difficulty.For(_level);
		}
		SetFlagMode(false);
		StartNewBoard();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		// Safety net for any way of leaving this page that is not the confirmed OnBackClicked/hardware-back
		// path below (app put in background, OS reclaiming memory, etc.) -- a mid-game save must never
		// depend on the player having explicitly confirmed an exit.
		SaveProgressIfPlaying();
	}

	// Android hardware/gesture back: OnBackButtonPressed is synchronous and cannot await a dialog, so it
	// blocks the default pop (returns true) and drives the same confirm-then-leave flow manually.
	protected override bool OnBackButtonPressed()
	{
		if (IsMidGame())
		{
			_ = ConfirmAndLeaveAsync();
			return true;
		}
		return base.OnBackButtonPressed();
	}

	private bool IsMidGame() => _boardStarted && _engine is { Status: GameStatus.Playing };

	// ===== Board setup =====

	private void StartNewBoard()
	{
		_saves.Clear(); // a fresh board replaces whatever was saved for the previous one
		_boardStarted = false;
		_engine = null;
		DailyBadge.IsVisible = _isDaily;
		ShowPlayingControls();
		StopTimer();
		_elapsedSeconds = 0;
		TimerLabel.Text = "0:00";
		MineCountLabel.Text = _difficulty.MineCount.ToString();

		BuildEmptyBoard();

		if (_isDaily)
		{
			Board? board = DailyChallenge.BoardFor(DateOnly.FromDateTime(DateTime.UtcNow));
			if (board == null)
			{
				// Astronomically unlikely (plan section 3.3) -- fall back to a normal Medium game rather
				// than leave the player stuck with no board at all.
				_isDaily = false;
			}
			else
			{
				_engine = new GameEngine(board);
				_boardStarted = true;
				int cx = _difficulty.Width / 2, cy = _difficulty.Height / 2;
				_engine.Reveal(cx, cy);
				RefreshAllCells();
				StartTimer();
			}
		}
	}

	private void ResumeFrom(SavedGame save)
	{
		_level = (DifficultyLevel)save.DifficultyLevel;
		_noGuess = save.NoGuess;
		_isDaily = save.IsDaily;
		_levelNumber = save.LevelNumber;
		// The real Difficulty (not a synthetic one built from the save's width/height alone), so that
		// MineCount is correct too if the player hits "Chơi lại" right after resuming.
		_difficulty = _levelNumber > 0 ? LevelCatalog.DifficultyFor(_levelNumber)
			: _isDaily ? DailyChallenge.Difficulty
			: Difficulty.For(_level);

		DailyBadge.IsVisible = _isDaily;
		SetFlagMode(false);
		BuildEmptyBoard();
		_engine = SavedGameStore.ToEngine(save);
		_boardStarted = true;
		_elapsedSeconds = save.ElapsedSeconds;
		TimerLabel.Text = $"{_elapsedSeconds / 60}:{_elapsedSeconds % 60:00}";
		RefreshAllCells();

		if (_engine.Status == GameStatus.Playing) StartTimer();
		else ShowPlayingControls(); // a saved game is never kept once Won/Lost (see HandleGameEndIfNeeded) -- defensive only
	}

	private void BuildEmptyBoard()
	{
		BoardHost.Children.Clear();
		BoardHost.RowDefinitions.Clear();
		BoardHost.ColumnDefinitions.Clear();

		double cellSize = _difficulty.Width > 20 ? 26 : 38;

		for (int x = 0; x < _difficulty.Width; x++) BoardHost.ColumnDefinitions.Add(new ColumnDefinition(cellSize));
		for (int y = 0; y < _difficulty.Height; y++) BoardHost.RowDefinitions.Add(new RowDefinition(cellSize));

		_cellBorders = new Border[_difficulty.Width, _difficulty.Height];
		_cellLabels = new Label[_difficulty.Width, _difficulty.Height];
		_priorCellState = new CellState?[_difficulty.Width, _difficulty.Height];

		for (int x = 0; x < _difficulty.Width; x++)
		{
			for (int y = 0; y < _difficulty.Height; y++)
			{
				var label = new Label
				{
					HorizontalOptions = LayoutOptions.Center,
					VerticalOptions = LayoutOptions.Center,
					FontFamily = "OpenSansSemibold",
					FontSize = _difficulty.Width > 20 ? 12 : 15,
				};
				var border = new Border
				{
					BackgroundColor = (Color)Application.Current!.Resources["CellHidden"],
					Stroke = (Color)Application.Current.Resources["MilkBackground"],
					StrokeThickness = 1,
					StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 3 },
					Content = label,
					Padding = 0,
					// A subtle raised-tile shadow, visible while hidden; RefreshAllCells turns it off the
					// moment a cell is revealed so the board reads as "this one is now flat/open" at a
					// glance, not just by its color changing.
					Shadow = HiddenShadow,
				};

				// ONE TouchBehavior only, for both tap and long-press: a TapGestureRecognizer added
				// alongside TouchBehavior on the same view does not fire on Android (TouchBehavior owns the
				// touch stream first) -- confirmed on a real device 2026-10-17, every cell went dead the
				// moment long-press was added via a second, separate TapGestureRecognizer. Chord (the
				// "double-tap a revealed number" shortcut) is therefore detected by hand in OnCellPrimaryTap
				// instead of a second TapGestureRecognizer: two Command hits on the same cell inside
				// DoubleTapWindow count as a chord, without delaying the (far more common) single tap at all.
				int cx = x, cy = y; // capture
				border.Behaviors.Add(new TouchBehavior
				{
					Command = new Command(() => OnCellPrimaryTap(cx, cy)),
					LongPressCommand = new Command(() => OnCellLongPress(cx, cy)),
					// Instant feedback the instant a finger lands, before Reveal/ToggleFlag even runs --
					// research into this genre consistently flags silence-until-result as the thing that
					// makes touch controls feel unresponsive, even when the underlying action is fast.
					PressedBackgroundColor = (Color)Application.Current!.Resources["CellHiddenPressed"],
				});

				Grid.SetColumn(border, x);
				Grid.SetRow(border, y);
				BoardHost.Children.Add(border);
				_cellBorders[x, y] = border;
				_cellLabels[x, y] = label;
			}
		}
	}

	// ===== Input =====

	// Chord detection (plan section 5's double-tap, now hand-rolled -- see BuildEmptyBoard's comment on
	// why a second TapGestureRecognizer cannot be used): two OnCellPrimaryTap hits on the SAME cell within
	// this window count as a chord. 350ms matches Android's own double-tap timeout ballpark.
	private static readonly TimeSpan DoubleTapWindow = TimeSpan.FromMilliseconds(350);
	private (int X, int Y)? _lastTapCell;
	private DateTime _lastTapUtc;

	private void OnCellPrimaryTap(int x, int y)
	{
		if (!_boardStarted)
		{
			StartGameFrom(x, y);
			return;
		}

		if (_engine is not { Status: GameStatus.Playing } engine) return;

		DateTime now = DateTime.UtcNow;
		bool isChord = _lastTapCell == (x, y) && now - _lastTapUtc < DoubleTapWindow;
		_lastTapCell = isChord ? null : (x, y); // reset on chord so a 3rd quick tap starts fresh, not another chord
		_lastTapUtc = now;

		if (isChord) engine.Chord(x, y);
		else if (_flagMode) engine.ToggleFlag(x, y);
		else engine.Reveal(x, y);

		RefreshAllCells();
		HandleGameEndIfNeeded();
		SaveProgressIfPlaying();
	}

	private void OnCellLongPress(int x, int y)
	{
		if (!_boardStarted) return;
		if (_engine is not { Status: GameStatus.Playing } engine) return;
		engine.ToggleFlag(x, y);
		RefreshAllCells();
		SaveProgressIfPlaying();
	}

	private void StartGameFrom(int firstX, int firstY)
	{
		Board board = _noGuess
			? BoardGenerator.GenerateNoGuess(_difficulty, firstX, firstY, Random.Shared) ?? BoardGenerator.Generate(_difficulty, firstX, firstY, Random.Shared)
			: BoardGenerator.Generate(_difficulty, firstX, firstY, Random.Shared);

		_engine = new GameEngine(board);
		_boardStarted = true;
		_engine.Reveal(firstX, firstY);
		RefreshAllCells();
		StartTimer();
		HandleGameEndIfNeeded();
		SaveProgressIfPlaying();
	}

	// ===== Save/resume =====

	private void SaveProgressIfPlaying()
	{
		if (_engine is not { Status: GameStatus.Playing } engine || !_boardStarted) return;
		_saves.Save(engine, _elapsedSeconds, _isDaily, _noGuess, _level, _levelNumber);
	}

	// ===== Rendering =====

	private void RefreshAllCells()
	{
		if (_engine is not { } engine || _cellBorders == null || _cellLabels == null || _priorCellState == null) return;

		var hiddenColor = (Color)Application.Current!.Resources["CellHidden"];
		var revealedColor = (Color)Application.Current.Resources["CellRevealed"];
		var mineColor = (Color)Application.Current.Resources["CellMine"];

		// Cells that just transitioned into Revealed/Flagged this pass get a quick pop (ScaleTo+FadeTo in
		// AnimatePoppedCellsAsync below). Collected rather than started inline so a single flood-fill reveal
		// -- which can touch the whole board at once on Hard -- is counted first; past PopAnimationCap, the
		// pop is skipped and cells just snap straight to their final look, trading the flourish for guaranteed
		// smooth frame pacing on a big reveal (there's no device here to verify many-cells-at-once GPU cost).
		List<Border> justAppeared = [];

		for (int x = 0; x < engine.Width; x++)
		{
			for (int y = 0; y < engine.Height; y++)
			{
				Border border = _cellBorders[x, y];
				Label label = _cellLabels[x, y];
				CellState state = engine.StateAt(x, y);
				bool isNewlyShown = state != _priorCellState[x, y] && state is CellState.Revealed or CellState.Flagged;

				switch (state)
				{
					case CellState.Hidden:
						border.BackgroundColor = hiddenColor;
						border.Shadow = HiddenShadow;
						label.Text = string.Empty;
						break;
					case CellState.Flagged:
						border.BackgroundColor = hiddenColor;
						border.Shadow = HiddenShadow;
						label.FontFamily = "Icons"; // Material glyph (Views/Icons.cs) -- no font-icon exists for a mine, that one case stays the 💣 emoji below
						label.Text = Icons.Flag;
						label.TextColor = (Color)Application.Current!.Resources["Accent"];
						break;
					case CellState.Revealed:
						int number = engine.NumberAt(x, y);
						border.Shadow = null!; // flat the instant it opens: null is valid at runtime to clear a Border's shadow, just not reflected in its (non-nullable) property type
						if (number == -1)
						{
							border.BackgroundColor = mineColor;
							label.FontFamily = "OpenSansSemibold"; // emoji renders regardless of FontFamily; this just resets it for the next cell that reuses this Label as a number
							label.Text = "💣";
						}
						else
						{
							border.BackgroundColor = revealedColor;
							label.FontFamily = "OpenSansSemibold";
							label.Text = number > 0 ? number.ToString() : string.Empty;
							label.TextColor = NumberColor(number);
						}
						break;
				}

				if (isNewlyShown)
				{
					// Set synchronously now, before the pop/skip decision below is even made, so the cell
					// never flashes at its resting scale/opacity for one frame first.
					border.Scale = CellPopStartScale;
					border.Opacity = 0;
					justAppeared.Add(border);
				}
				_priorCellState[x, y] = state;
			}
		}

		if (justAppeared.Count > 0)
		{
			if (justAppeared.Count <= PopAnimationCap)
			{
				foreach (Border border in justAppeared) _ = AnimateCellPopAsync(border);
			}
			else
			{
				foreach (Border border in justAppeared) { border.Scale = 1; border.Opacity = 1; }
			}
		}

		MineCountLabel.Text = engine.MinesRemaining.ToString();
	}

	private const double CellPopStartScale = 0.8;
	private const int PopAnimationCap = 60;

	private static async Task AnimateCellPopAsync(Border border)
	{
		await Task.WhenAll(
			border.ScaleToAsync(1, 140, Easing.CubicOut),
			border.FadeToAsync(1, 110, Easing.CubicOut));
	}

	private static Color NumberColor(int number) => number switch
	{
		1 => (Color)Application.Current!.Resources["Number1"],
		2 => (Color)Application.Current!.Resources["Number2"],
		3 => (Color)Application.Current!.Resources["Number3"],
		4 => (Color)Application.Current!.Resources["Number4"],
		5 => (Color)Application.Current!.Resources["Number5"],
		6 => (Color)Application.Current!.Resources["Number6"],
		7 => (Color)Application.Current!.Resources["Number7"],
		_ => (Color)Application.Current!.Resources["Number8"],
	};

	// ===== Timer =====

	private void StartTimer()
	{
		StopTimer();
		_timer = Dispatcher.CreateTimer();
		_timer.Interval = TimeSpan.FromSeconds(1);
		_timer.Tick += (_, _) =>
		{
			_elapsedSeconds++;
			TimerLabel.Text = $"{_elapsedSeconds / 60}:{_elapsedSeconds % 60:00}";
		};
		_timer.Start();
	}

	private void StopTimer()
	{
		_timer?.Stop();
		_timer = null;
	}

	// ===== Game end =====

	// Round-end state: board stays fully visible and frozen (OnCellPrimaryTap/OnCellLongPress already bail
	// out once _engine.Status != Playing, nothing extra needed there), ResultBanner replaces the flag
	// toggle in the same row, and Hint/Restart hide -- the top bar's own back button is the only control
	// left, per the owner's "chỉ có một button là back" direction (2026-10-19).
	private void ShowResultBanner()
	{
		FlagToggleRow.IsVisible = false;
		HintButton.IsVisible = false;
		RestartButton.IsVisible = false;
		ResultBanner.IsVisible = true;
	}

	private void ShowPlayingControls()
	{
		ResultBanner.IsVisible = false;
		FlagToggleRow.IsVisible = true;
		HintButton.IsVisible = true;
		RestartButton.IsVisible = true;
	}

	private void HandleGameEndIfNeeded()
	{
		if (_engine is not { } engine || engine.Status == GameStatus.Playing) return;

		StopTimer();
		_saves.Clear(); // a finished game is never resumable
		_ads.RegisterGameEnd();

		if (engine.Status == GameStatus.Won)
		{
			ResultTitleLabel.Text = "Bạn đã thắng!";

			if (_levelNumber > 0)
			{
				int stars = LevelStars.StarsFor(_levelNumber, _elapsedSeconds);
				_levels.RecordCompletion(_levelNumber, stars);
				StarsLabel.Text = new string('★', stars) + new string('☆', 3 - stars);
				StarsLabel.IsVisible = true;
				ResultDetailLabel.Text = $"Màn {_levelNumber} · {_elapsedSeconds / 60}:{_elapsedSeconds % 60:00}";
			}
			else
			{
				bool isRecord = !_isDaily && _stats.RecordWin(_level, _elapsedSeconds);
				StarsLabel.IsVisible = false;
				ResultDetailLabel.Text = isRecord
					? $"Kỷ lục mới: {_elapsedSeconds / 60}:{_elapsedSeconds % 60:00}"
					: $"Thời gian: {_elapsedSeconds / 60}:{_elapsedSeconds % 60:00}";
			}
		}
		else
		{
			ResultTitleLabel.Text = "Trúng mìn rồi!";
			ResultDetailLabel.Text = $"Thời gian: {_elapsedSeconds / 60}:{_elapsedSeconds % 60:00}";
			StarsLabel.IsVisible = false;
		}

		ShowResultBanner();
	}

	// ===== Toolbar =====

	private void OnHintClicked(object? sender, EventArgs e)
	{
		if (_engine is not { Status: GameStatus.Playing } engine || _cellBorders == null) return;
		(int X, int Y)? hint = engine.FindHint();
		if (hint is not { } cell) return;

		Border border = _cellBorders[cell.X, cell.Y];
		border.Stroke = (Color)Application.Current!.Resources["Accent"];
		border.StrokeThickness = 3;
	}

	private async void OnRestartClicked(object? sender, EventArgs e)
	{
		bool confirm = await DisplayAlertAsync("Chơi lại?", "Bàn hiện tại sẽ mất.", "Chơi lại", "Huỷ");
		if (confirm) StartNewBoard();
	}

	private void OnRevealModeClicked(object? sender, EventArgs e) => SetFlagMode(false);
	private void OnFlagModeClicked(object? sender, EventArgs e) => SetFlagMode(true);

	private void SetFlagMode(bool flagMode)
	{
		_flagMode = flagMode;
		var primary = (Color)Application.Current!.Resources["Primary"];
		RevealModeButton.BackgroundColor = flagMode ? Colors.Transparent : primary;
		RevealModeButton.TextColor = flagMode ? primary : Colors.White;
		FlagModeButton.BackgroundColor = flagMode ? primary : Colors.Transparent;
		FlagModeButton.TextColor = flagMode ? Colors.White : primary;
	}

	private async void OnBackClicked(object? sender, EventArgs e) => await ConfirmAndLeaveAsync();

	private async Task ConfirmAndLeaveAsync()
	{
		if (IsMidGame())
		{
			bool leave = await DisplayAlertAsync(
				"Thoát ván đang chơi?",
				"Tiến trình sẽ được lưu lại, bạn có thể vào lại chơi tiếp từ trang chủ.",
				"Thoát", "Ở lại");
			if (!leave) return;
			SaveProgressIfPlaying();
		}

		StopTimer();
		await Shell.Current.GoToAsync("..");
	}
}
