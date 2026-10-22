using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Behaviors;
using CommunityToolkit.Maui.Core;
using Minesweeper.Core;
using Minesweeper.Services;

namespace Minesweeper.Views;

[QueryProperty(nameof(DifficultyParam), "difficulty")]
[QueryProperty(nameof(DailyParam), "daily")]
[QueryProperty(nameof(DailyDateParam), "dailyDate")]
[QueryProperty(nameof(ResumeParam), "resume")]
[QueryProperty(nameof(LevelParam), "level")]
public partial class GamePage : ContentPage
{
	private readonly GameStatsStore _stats;
	private readonly IAdsService _ads;
	private readonly SavedGameStore _saves;
	private readonly LevelProgressStore _levels;
	private readonly ISoundService _sound;
	private readonly DailyProgressStore _dailyProgress;
	private readonly ActionGuard _nav = new();
	// Separate from _nav: _nav is held across OnRestartClicked's confirm dialog + its own call into
	// StartNewBoard, so if board generation used _nav too, a Daily restart would deadlock against itself
	// (TryEnter already held, by the very call that is trying to start the new board it guards).
	private readonly ActionGuard _boardGen = new();

	private DifficultyLevel _level = DifficultyLevel.Easy;
	// No longer a player-facing toggle (owner's direction, 2026-10-21: removed from HomePage) -- true for
	// everything except classic Hard, which measured as a reliable No-guess failure at 99 mines/288 cells
	// (10/10 attempts, even at the real 400-attempt budget) and keeps pure random generation instead. Set in
	// OnAppearing from _level/_isDaily/_levelNumber; ResumeFrom overrides it from the save's own NoGuess flag.
	private bool _noGuess;
	private bool _isDaily;
	// Which calendar date's puzzle this is, when _isDaily -- null means "today" (the normal "Thử thách hôm
	// nay" card on HomePage never sets this param at all). Set when DailyCalendarPage opens a past day's
	// puzzle, so completion gets recorded against THAT date, not whatever day it happens to be played on.
	private DateOnly? _dailyDate;
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
	// The dimensions/cell size BuildEmptyBoard last actually built the Border/Label tree for -- a "Chơi lại"
	// restart of the same difficulty recomputes to the exact same values (BoardArea never resizes mid-session),
	// so BuildEmptyBoard reuses the existing ~480-cell view tree instead of tearing it down and reallocating
	// every native Android view on the UI thread (owner's report, 2026-10-20: game start/restart felt slow).
	private int _builtWidth = -1, _builtHeight = -1;
	private double _builtCellSize = -1;
	private bool _loaded;

	private IDispatcherTimer? _timer;
	private int _elapsedSeconds;

	// Cancelled in OnDisappearing: without this, navigating away WHILE board generation is still running in
	// the background (StartGameFromAsync's Task.Run) left that Task holding this whole page -- its cell view
	// tree, GameEngine, everything -- alive until generation happened to finish on its own, then mutated a
	// page already off-screen (found in review, 2026-10-20).
	private readonly CancellationTokenSource _lifetimeCts = new();

	// Shared instance: a Shadow is a reference type and MAUI is fine with the same one assigned to many
	// Border.Shadow properties, so this avoids one allocation per cell per refresh on a 480-cell Hard board.
	private static readonly Shadow HiddenShadow = new() { Brush = Colors.Black, Opacity = 0.32f, Radius = 5, Offset = new Point(0, 2.5) };

	// One GraphicsView-backed ParticleField each for the mine-burst debris and the win confetti -- see the
	// ParticleField class comment (near AnimateMineExplosionRippleAsync) for why this replaced an earlier
	// one-native-BoxView-per-particle version. Assigned in the constructor, once MineBurstLayer/ConfettiLayer
	// exist (right after InitializeComponent), not inline field initializers -- those run before
	// InitializeComponent has created the XAML-named GraphicsViews they wrap.
	private ParticleField _mineBurstField = null!;
	private ParticleField _confettiField = null!;

	public string DifficultyParam
	{
		set => Enum.TryParse(Uri.UnescapeDataString(value), out _level);
	}

	public string DailyParam
	{
		set => bool.TryParse(Uri.UnescapeDataString(value), out _isDaily);
	}

	public string DailyDateParam
	{
		set => _dailyDate = DateOnly.TryParse(Uri.UnescapeDataString(value), out DateOnly date) ? date : null;
	}

	public string ResumeParam
	{
		set => bool.TryParse(Uri.UnescapeDataString(value), out _resume);
	}

	public string LevelParam
	{
		set => int.TryParse(Uri.UnescapeDataString(value), out _levelNumber);
	}

	public GamePage(GameStatsStore stats, IAdsService ads, SavedGameStore saves, LevelProgressStore levels, ISoundService sound, DailyProgressStore dailyProgress)
	{
		InitializeComponent();
		_stats = stats;
		_ads = ads;
		_saves = saves;
		_levels = levels;
		_sound = sound;
		_dailyProgress = dailyProgress;
		// AdBannerSurface sizes itself to the real native ad view and claims NO space at all until one has
		// actually loaded (its own doc comment) -- fine on most pages, but here BuildEmptyBoard measures
		// BoardArea's height before that load finishes, so the board claimed the banner's space too and the
		// banner popped in on top of/overlapping it a moment later (owner's report, 2026-10-21). Reserving a
		// fixed height up front, before any board sizing happens, keeps BoardArea's own math correct from the
		// start -- 0 for a Pro/ads-disabled player, who will never see a banner at all.
		Banner.HeightRequest = _ads.ShowAds ? 60 : 0;
		BoardArea.SizeChanged += OnBoardAreaSizeChanged;
		_mineBurstField = new ParticleField(MineBurstLayer);
		_confettiField = new ParticleField(ConfettiLayer);
	}

	// ===== Board sizing =====

	// BoardArea.Width/Height read -1 (not yet measured) for a little while after OnAppearing first runs --
	// Shell's page-transition means the layout pass that gives BoardArea its real pixel size lands AFTER
	// OnAppearing returns, not before. StartNewBoard/ResumeFrom route everything that depends on a real cell
	// size through this instead of calling it directly, so the board gets built against BoardArea's actual
	// measured size (whatever that turns out to be) rather than a guess.
	private Action? _pendingBoardSetup;

	// Dispatcher.Dispatch (next UI loop tick), not a direct call, even on the "already sized" path: BuildEmptyBoard
	// synchronously creates up to 288 native Border/Label/TouchBehavior views, and running that inline inside
	// OnAppearing's own call stack competed with Shell's still-settling page-transition frame for the UI
	// thread, which read as the "Chơi" button taking 1-2s to actually respond (owner's report, 2026-10-22).
	// Posting it instead lets the transition finish and the loading overlay (shown synchronously in
	// StartNewBoard/ResumeFrom, before this ever runs) actually paint first.
	private void RunWhenBoardAreaSized(Action action)
	{
		if (BoardArea.Width > 0 && BoardArea.Height > 0) { Dispatcher.Dispatch(action); return; }
		_pendingBoardSetup = action;
	}

	private void OnBoardAreaSizeChanged(object? sender, EventArgs e)
	{
		if (_pendingBoardSetup is not { } action) return;
		if (BoardArea.Width <= 0 || BoardArea.Height <= 0) return;
		_pendingBoardSetup = null;
		Dispatcher.Dispatch(action);
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

		if (_levelNumber > 0) _isDaily = false;
		// No-guess for everything except classic Hard (see the _noGuess field comment) -- Tutorial/Daily are
		// always No-guess regardless of _level, which is why those two are checked first.
		_noGuess = _levelNumber > 0 || _isDaily || _level != DifficultyLevel.Hard;
		_difficulty = CurrentPresetDifficulty();
		StartNewBoard();
	}

	// Today's real preset for whatever this page is currently set to play (Tutorial level / Daily / a
	// classic difficulty) -- used for a fresh start AND for "Chơi lại" after a resume (see ResumeFrom's own
	// comment on why a resumed save's _difficulty must NOT stay as this).
	private Difficulty CurrentPresetDifficulty() => _levelNumber > 0 ? LevelCatalog.DifficultyFor(_levelNumber)
		: _isDaily ? DailyChallenge.Difficulty
		: Difficulty.For(_level);

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		// Safety net for any way of leaving this page that is not the confirmed OnBackClicked/hardware-back
		// path below (app put in background, OS reclaiming memory, etc.) -- a mid-game save must never
		// depend on the player having explicitly confirmed an exit. StopTimer here too: ConfirmAndLeaveAsync
		// already stops it on the confirmed paths, but without this, any OTHER way off this page left the
		// IDispatcherTimer ticking once a second forever -- it, and everything its closure keeps alive (this
		// whole page, its ~190-cell Border/Label/TouchBehavior tree, the GameEngine), never became eligible
		// for GC (found in review, 2026-10-20).
		StopTimer();
		SaveProgressIfPlaying();
		_lifetimeCts.Cancel();
	}

	// Android hardware/gesture back: OnBackButtonPressed is synchronous and cannot await a dialog, so it
	// blocks the default pop (returns true) and drives the same confirm-then-leave flow manually.
	protected override bool OnBackButtonPressed()
	{
		if (IsMidGame())
		{
			// Shares _nav with OnBackClicked (below): a hardware-back mashed alongside the on-screen back
			// button must not show the leave-confirm dialog twice or race two GoToAsync(".." ) pops.
			if (_nav.TryEnter()) _ = LeaveAndExitGuardAsync();
			return true;
		}
		return base.OnBackButtonPressed();
	}

	private async Task LeaveAndExitGuardAsync()
	{
		try { await ConfirmAndLeaveAsync(); }
		finally { _nav.Exit(); }
	}

	// _timer != null, not just "the engine exists": the board/mines are generated immediately now (see
	// StartNewBoard/GenerateBoardAsync), before the player has done anything -- "mid-game, worth confirming
	// an exit or resuming later" should still mean "the player has actually made a move", which is exactly
	// when the timer starts (OnCellPrimaryTap/OnCellLongPress).
	private bool IsMidGame() => _timer != null && _engine is { Status: GameStatus.Playing };

	// ===== Board setup =====

	private void StartNewBoard()
	{
		_saves.Clear(); // a fresh board replaces whatever was saved for the previous one
		_engine = null;
		DailyBadge.IsVisible = _isDaily;
		ShowPlayingControls();
		StopTimer();
		_elapsedSeconds = 0;
		TimerLabel.Text = "0:00";
		MineCountLabel.Text = _difficulty.MineCount.ToString();
		// Shown immediately, not just inside GenerateBoardAsync: covers BuildEmptyBoard's own synchronous
		// native-view creation too, so the player sees "Đang tạo bàn chơi…" the instant this page appears
		// instead of a blank/frozen-looking board for however long that takes (owner's report, 2026-10-22).
		ShowBoardLoading(true);

		RunWhenBoardAreaSized(() =>
		{
			BuildEmptyBoard();
			if (_boardGen.TryEnter()) _ = GenerateBoardAsync();
		});
	}

	private void ResumeFrom(SavedGame save)
	{
		_level = (DifficultyLevel)save.DifficultyLevel;
		_noGuess = save.NoGuess;
		_isDaily = save.IsDaily;
		_levelNumber = save.LevelNumber;
		// The save's OWN dimensions/mine count, NOT today's live preset: a preset's Width/Height can change
		// between app versions (exactly what happened when Difficulty.Medium/Hard were resized 2026-10-20) --
		// BuildEmptyBoard sizes the cell grid from _difficulty, while _engine below is rebuilt from the save's
		// real dimensions; if those two disagree, RefreshAllCells indexes _cellBorders past its bounds and
		// crashes the instant a resumed game renders. "Chơi lại" (OnRestartClicked) re-derives _difficulty
		// from CurrentPresetDifficulty() instead, so a restart still gets today's real difficulty.
		_difficulty = new Difficulty("Tiếp tục", save.Width, save.Height, save.MinesFlat.Count(m => m));

		DailyBadge.IsVisible = _isDaily;
		ShowBoardLoading(true); // same reasoning as StartNewBoard -- covers BuildEmptyBoard's own cost too

		RunWhenBoardAreaSized(() =>
		{
			BuildEmptyBoard();
			_engine = SavedGameStore.ToEngine(save);
			_elapsedSeconds = save.ElapsedSeconds;
			TimerLabel.Text = $"{_elapsedSeconds / 60}:{_elapsedSeconds % 60:00}";
			RefreshAllCells();

			if (_engine.Status == GameStatus.Playing) StartTimer();
			else ShowPlayingControls(); // a saved game is never kept once Won/Lost (see HandleGameEndIfNeeded) -- defensive only
			ShowBoardLoading(false); // no GenerateBoardAsync in this path to turn it off on its own
		});
	}

	// Hard floor only (never the design target): guarantees availableWidth/Height and the final cell size can
	// never divide out to zero/negative even in a pathological measurement. BoardAreaMargin is the "margin
	// 10px về 4 cạnh" the owner asked for -- taken out of the available space BEFORE dividing by Width/Height,
	// not added as XAML Padding afterwards, or a board sized to exactly fill the screen would overflow it by
	// the padding amount. BoardCellMaxSize keeps Easy/Medium compact and "classic-sized" rather than stretching
	// -- Hard's own raw size very rarely reaches it on a real phone, so Hard still ends up ~filling the screen
	// per the "ở loại khó thì full width/height" direction, without a separate Hard-only code path.
	private const double BoardCellMinSize = 4;
	private const double BoardCellMaxSize = 42;
	private const double BoardAreaMargin = 10;

	private void BuildEmptyBoard()
	{
		double availableWidth = Math.Max(1, BoardArea.Width - BoardAreaMargin * 2);
		double availableHeight = Math.Max(1, BoardArea.Height - BoardAreaMargin * 2);
		double cellSize = Math.Clamp(
			Math.Min(availableWidth / _difficulty.Width, availableHeight / _difficulty.Height),
			BoardCellMinSize, BoardCellMaxSize);
		double fontSize = Math.Clamp(cellSize * 0.42, 9, 17);

		// Same grid as the last build -- reuse it (see the field comment on _builtWidth) instead of
		// rebuilding. Just needs its visuals snapped back to a fresh Hidden look; RefreshAllCells repaints
		// everything else (revealed/flagged/mine) the moment a real move happens on the new board.
		if (_cellBorders != null && _builtWidth == _difficulty.Width && _builtHeight == _difficulty.Height && _builtCellSize == cellSize)
		{
			ResetCellsToHidden();
			return;
		}

		BoardHost.Children.Clear();
		BoardHost.RowDefinitions.Clear();
		BoardHost.ColumnDefinitions.Clear();

		for (int x = 0; x < _difficulty.Width; x++) BoardHost.ColumnDefinitions.Add(new ColumnDefinition(cellSize));
		for (int y = 0; y < _difficulty.Height; y++) BoardHost.RowDefinitions.Add(new RowDefinition(cellSize));

		_cellBorders = new Border[_difficulty.Width, _difficulty.Height];
		_cellLabels = new Label[_difficulty.Width, _difficulty.Height];
		_priorCellState = new CellState?[_difficulty.Width, _difficulty.Height];

		// Deliberately BackgroundColor, not a gradient Background Brush: CommunityToolkit's TouchBehavior
		// (below) only ever writes PressedBackgroundColor into .BackgroundColor (confirmed by inspecting
		// GestureManager in the installed package, 2026-10-20 -- it has no Brush-aware counterpart), and a
		// non-null Border.Background Brush always wins over BackgroundColor when both are set. A gradient
		// fill here would render fine at rest but silently swallow the press-flash every time -- the "raised"
		// look instead comes from HiddenRaisedStroke + HiddenShadow below, which don't touch either property.
		Color hiddenColor = (Color)Application.Current!.Resources["CellHidden"];

		for (int x = 0; x < _difficulty.Width; x++)
		{
			for (int y = 0; y < _difficulty.Height; y++)
			{
				var label = new Label
				{
					HorizontalOptions = LayoutOptions.Center,
					VerticalOptions = LayoutOptions.Center,
					FontFamily = "OpenSansSemibold",
					FontSize = fontSize,
				};
				var border = new Border
				{
					BackgroundColor = hiddenColor,
					Stroke = hiddenColor.AddLuminosity(0.22f),
					StrokeThickness = 1.5,
					StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 },
					Content = label,
					Padding = 0,
					// A beveled-highlight edge (Stroke, above) + a stronger drop shadow (below) stand in for a
					// real raised-button bevel (owner's direction, 2026-10-20: a hidden cell should read as a
					// physical, raised tile, not a flat sharp-cornered square) -- RefreshAllCells turns the
					// shadow off the moment a cell is revealed so the board reads as "this one is now flat/
					// open" at a glance, not just by its color changing.
					Shadow = HiddenShadow,
				};

				// ONE TouchBehavior only, for both tap and long-press: a TapGestureRecognizer added
				// alongside TouchBehavior on the same view does not fire on Android (TouchBehavior owns the
				// touch stream first) -- confirmed on a real device 2026-10-17, every cell went dead the
				// moment long-press was added via a second, separate TapGestureRecognizer. Chord (opening
				// every unflagged neighbor of an already-satisfied number) is just a single tap on that
				// already-revealed cell -- see OnCellPrimaryTap -- so no second gesture is needed for it at all.
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

		_builtWidth = _difficulty.Width;
		_builtHeight = _difficulty.Height;
		_builtCellSize = cellSize;
	}

	// Mirrors exactly what a freshly-constructed cell above starts out as (BackgroundColor/Stroke/Shadow
	// hidden, empty label, resting Scale/Opacity) -- RefreshAllCells's own CellState.Hidden branch does the
	// same thing but only reaches cells once a move actually happens, and this must be visible immediately,
	// before the player's first tap, when BuildEmptyBoard reuses a view tree that still shows the previous
	// game's revealed/flagged/mine state.
	private void ResetCellsToHidden()
	{
		Color hiddenColor = (Color)Application.Current!.Resources["CellHidden"];
		Color hiddenStroke = hiddenColor.AddLuminosity(0.22f);
		for (int x = 0; x < _difficulty.Width; x++)
		{
			for (int y = 0; y < _difficulty.Height; y++)
			{
				Border border = _cellBorders![x, y];
				border.BackgroundColor = hiddenColor;
				border.Stroke = hiddenStroke;
				border.Shadow = HiddenShadow;
				border.Scale = 1;
				border.Opacity = 1;
				_cellLabels![x, y].Text = string.Empty;
				_priorCellState![x, y] = null;
			}
		}
	}

	// ===== Input =====

	private void OnCellPrimaryTap(int x, int y)
	{
		// The board/mines already exist by the time any tap can land (GenerateBoardAsync runs the instant
		// the page opens, see StartNewBoard) -- a board still generating means _engine is null, so this cell
		// just isn't live yet. No special-casing for "the first tap" any more: it is a real Reveal exactly
		// like every later one, which is the whole point -- it can genuinely hit a mine (owner's direction,
		// 2026-10-21).
		if (_engine is not { Status: GameStatus.Playing } engine) return;
		if (_timer == null) StartTimer(); // first real move of this board starts the clock

		// Chord is just a single tap on an already-revealed number now (owner's direction, 2026-10-22,
		// matching how other mobile Minesweeper apps do it): a revealed cell has no other use for a tap, so
		// there is no ambiguity, and this replaces the old hand-rolled double-tap-within-a-window gesture
		// entirely -- GameEngine.Chord already no-ops quietly if the flag count doesn't match yet, same as
		// before.
		bool isChord = engine.StateAt(x, y) == CellState.Revealed;
		if (isChord) engine.Chord(x, y);
		else engine.Reveal(x, y);
		PlayActionFeedback(engine, isFlagAction: false);

		if (engine.Status == GameStatus.Lost)
		{
			// Loss: every other-than-the-tapped mine must NOT snap to its revealed look in this same
			// RefreshAllCells pass (deferMineReveal) -- AnimateMineExplosionRippleAsync below stages them
			// into the actual blast, and HandleGameEndIfNeeded/the ResultBanner popup must not appear until
			// that finishes (owner's direction, 2026-10-22).
			List<(Border Border, Label Label, int X, int Y)> mines = RefreshAllCells(deferMineReveal: true);
			_ = AnimateMineExplosionRippleAsync(mines, x, y);
		}
		else
		{
			RefreshAllCells();
			HandleGameEndIfNeeded();
			SaveProgressIfPlaying();
		}
	}

	private void OnCellLongPress(int x, int y)
	{
		// Flagging works the instant the board exists too (owner's direction, 2026-10-21) -- no reveal has
		// to happen first any more.
		if (_engine is not { Status: GameStatus.Playing } engine) return;
		if (_timer == null) StartTimer();
		engine.ToggleFlag(x, y);
		PlayActionFeedback(engine, isFlagAction: true);
		RefreshAllCells();
		SaveProgressIfPlaying();
	}

	// Reveal/flag/chord tick -- skipped on a loss (HandleGameEndIfNeeded's own explosion sound + strong
	// buzz takes over instead, so the player never hears a cheerful pop layered under the mine going off).
	// Still plays alongside a win (a quick pop right under the win fanfare reads fine, unlike a pop under an
	// explosion), so only GameStatus.Lost is excluded here.
	private void PlayActionFeedback(GameEngine engine, bool isFlagAction)
	{
		if (engine.Status == GameStatus.Lost) return;
		if (isFlagAction) _sound.PlayFlag();
		else _sound.PlayReveal();
		if (AppPreferences.VibrationEnabled) HapticFeedback.Default.Perform(HapticFeedbackType.Click);
	}

	// Generates the board -- mines included -- the instant the page is ready to show it, not deferred to the
	// player's first tap (owner's direction, 2026-10-21: the board/mine layout must already exist the moment
	// a level opens, so flagging works immediately and the very first tap is a real Reveal that can genuinely
	// hit a mine, same as every later one). No-guess generation (when _noGuess) can no longer anchor its
	// solvability proof on the player's own first tap -- there isn't one yet -- so it anchors on the board's
	// center instead; non-No-guess generation (BoardGenerator.GeneratePureRandom) has no anchor at all, which
	// is the actual point: giving up the "first click is always safe" guarantee is what was asked for.
	private async Task GenerateBoardAsync()
	{
		ShowBoardLoading(true);
		try
		{
			// Task.Run, not a direct call: No-guess generation can retry board layouts up to 400 times (each a
			// full solve simulation) before giving up, real CPU time on a slow device -- off the UI thread so
			// this doesn't read as the app hanging on page load (owner's report, 2026-10-20).
			Difficulty difficulty = _difficulty;
			bool noGuess = _noGuess;
			int cx = difficulty.Width / 2, cy = difficulty.Height / 2;
			Board board = await Task.Run(() => noGuess
				? BoardGenerator.GenerateNoGuess(difficulty, cx, cy, Random.Shared) ?? BoardGenerator.Generate(difficulty, cx, cy, Random.Shared)
				: BoardGenerator.GeneratePureRandom(difficulty, Random.Shared));
			if (_lifetimeCts.IsCancellationRequested) return; // player already left this page while this was generating

			_engine = new GameEngine(board);
			RefreshAllCells(); // paints the fresh, fully-hidden board
		}
		finally
		{
			ShowBoardLoading(false);
			_boardGen.Exit();
		}
	}

	private void ShowBoardLoading(bool loading)
	{
		BoardLoadingOverlay.IsVisible = loading;
		BoardLoadingSpinner.IsRunning = loading;
	}

	// ===== Save/resume =====

	// _timer != null, not just "the engine exists" -- same reasoning as IsMidGame: an untouched fresh board
	// (generated but not yet acted on) isn't worth saving, resuming it would be identical to starting fresh.
	private void SaveProgressIfPlaying()
	{
		if (_engine is not { Status: GameStatus.Playing } engine || _timer == null) return;
		_saves.Save(engine, _elapsedSeconds, _isDaily, _noGuess, _level, _levelNumber);
	}

	// ===== Rendering =====

	// deferMineReveal: true only for the one RefreshAllCells call that itself triggers a loss (see
	// OnCellPrimaryTap) -- every mine that would otherwise instantly snap to its revealed 💣 look in this
	// same pass is instead left painted as still-Hidden and returned to the caller, so
	// AnimateMineExplosionRippleAsync can stage them into an actual outward blast instead of a single
	// instant flash. Every other caller ignores the return value and gets the old all-at-once behavior.
	private List<(Border Border, Label Label, int X, int Y)> RefreshAllCells(bool deferMineReveal = false)
	{
		if (_engine is not { } engine || _cellBorders == null || _cellLabels == null || _priorCellState == null) return [];

		// See BuildEmptyBoard's comment on why this is BackgroundColor + Stroke, not a Background Brush:
		// TouchBehavior's PressedBackgroundColor only ever writes BackgroundColor, which a non-null Background
		// Brush would otherwise always render over.
		Color hiddenColor = (Color)Application.Current!.Resources["CellHidden"];
		Color hiddenStroke = hiddenColor.AddLuminosity(0.22f);
		var revealedColor = (Color)Application.Current.Resources["CellRevealed"];
		var mineColor = (Color)Application.Current.Resources["CellMine"];

		// Cells that just transitioned into Revealed/Flagged this pass get a quick pop (ScaleTo+FadeTo in
		// AnimatePoppedCellsAsync below). Collected rather than started inline so a single flood-fill reveal
		// -- which can touch the whole board at once on Hard -- is counted first; past PopAnimationCap, the
		// pop is skipped and cells just snap straight to their final look, trading the flourish for guaranteed
		// smooth frame pacing on a big reveal (there's no device here to verify many-cells-at-once GPU cost).
		List<Border> justAppeared = [];
		List<(Border Border, Label Label, int X, int Y)> deferredMines = [];

		for (int x = 0; x < engine.Width; x++)
		{
			for (int y = 0; y < engine.Height; y++)
			{
				Border border = _cellBorders[x, y];
				Label label = _cellLabels[x, y];
				CellState state = engine.StateAt(x, y);
				bool isNewlyShown = state != _priorCellState[x, y] && state is CellState.Revealed or CellState.Flagged;
				bool deferredThisCell = false;

				switch (state)
				{
					case CellState.Hidden:
						border.BackgroundColor = hiddenColor;
						border.Stroke = hiddenStroke;
						border.Shadow = HiddenShadow;
						label.Text = string.Empty;
						break;
					case CellState.Flagged:
						border.BackgroundColor = hiddenColor;
						border.Stroke = hiddenStroke;
						border.Shadow = HiddenShadow;
						label.FontFamily = "Icons"; // Material glyph (Views/Icons.cs) -- no font-icon exists for a mine, that one case stays the 💣 emoji below
						label.Text = Icons.Flag;
						label.TextColor = (Color)Application.Current!.Resources["Accent"];
						break;
					case CellState.Revealed:
						int number = engine.NumberAt(x, y);
						if (number == -1 && deferMineReveal && isNewlyShown)
						{
							// Left exactly as Hidden for now (engine already considers it Revealed) --
							// AnimateMineExplosionRippleAsync paints the real mine look + pop itself, staged
							// by ring, once this method returns.
							border.BackgroundColor = hiddenColor;
							border.Stroke = hiddenStroke;
							border.Shadow = HiddenShadow;
							label.Text = string.Empty;
							deferredMines.Add((border, label, x, y));
							deferredThisCell = true;
							break;
						}
						border.Stroke = (Color)Application.Current!.Resources["MilkBackground"]; // flat/sunken, not raised: back to the plain grid-line stroke BuildEmptyBoard no longer sets, since a revealed cell has no bevel
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

				if (isNewlyShown && !deferredThisCell)
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
		return deferredMines;
	}

	private const double CellPopStartScale = 0.8;
	private const int PopAnimationCap = 60;

	// A single combined Animation (not two independent ScaleToAsync/FadeToAsync calls via Task.WhenAll) --
	// found on a real device, 2026-10-20: two concurrent ...ToAsync calls on the SAME view intermittently left
	// Opacity stuck near 0 (cell content nearly invisible, permanently, not just mid-animation), on this MAUI
	// build (10.0.90). Animation.Add lets both properties share one Commit/one ticker instead of two competing
	// ones, and the `finished` callback is a hard safety net: whatever the interpolation did, Scale/Opacity
	// are forced to their real resting values the moment it ends, so this can never leave a cell stuck faded.
	private static Task AnimateCellPopAsync(Border border)
	{
		var tcs = new TaskCompletionSource();
		double startScale = border.Scale;
		double startOpacity = border.Opacity;
		var animation = new Animation();
		animation.Add(0, 1, new Animation(v => border.Scale = v, startScale, 1));
		animation.Add(0, 1, new Animation(v => border.Opacity = v, startOpacity, 1));
		animation.Commit(border, "CellPop", length: 140, easing: Easing.CubicOut, finished: (_, _) =>
		{
			border.Scale = 1;
			border.Opacity = 1;
			tcs.TrySetResult();
		});
		return tcs.Task;
	}

	// Hit a mine -> every other hidden mine on the board explodes outward from the tapped cell, ring by ring
	// (Chebyshev distance, matching the 8-neighbor adjacency GameEngine/Board already use everywhere else),
	// instead of RefreshAllCells's usual single instant pass. Each ring's cells are committed to AnimateCellPopAsync
	// -- a distinct Border per call, so the "two concurrent animations on the SAME view" bug that method's own
	// comment documents does not apply here -- and only the NEXT ring's start is staggered by Task.Delay, not
	// each ring's own pop completion, so rings visibly overlap/cascade like a real spreading blast rather than
	// pausing dead between each one. stepDelay is adaptive (MaxRippleTotalMs spread across however many rings
	// actually have a mine in them) so a Hard board, whose 12x24 grid can span up to ~24 distinct rings from a
	// corner tap, never drags the loss popup out past about a second, while a tight cluster of mines (few
	// rings) still gets a visible stagger instead of collapsing to nothing.
	private async Task AnimateMineExplosionRippleAsync(List<(Border Border, Label Label, int X, int Y)> mines, int originX, int originY)
	{
		if (mines.Count == 0) { _sound.PlayExplosion(); HandleGameEndIfNeeded(); SaveProgressIfPlaying(); return; }

		Color mineColor = (Color)Application.Current!.Resources["CellMine"];
		Color revealedStroke = (Color)Application.Current!.Resources["MilkBackground"];

		List<List<(Border Border, Label Label, int X, int Y)>> rings = mines
			.GroupBy(m => Math.Max(Math.Abs(m.X - originX), Math.Abs(m.Y - originY)))
			.OrderBy(g => g.Key)
			.Select(g => g.ToList())
			.ToList();

		int stepDelay = rings.Count <= 1 ? 0 : Math.Clamp(MaxRippleTotalMs / rings.Count, MinRingStepMs, MaxRingStepMs);

		// One explosion.wav per ring, not a single bang at the start (owner's follow-up, 2026-10-22: "mỗi quả
		// bom hiện là một tiếng nổ... tiếng nổ liên hoàn" -- a chained/cascading series, not one flat bang for
		// the whole event). Per-RING rather than per-MINE: a wide ring on a dense Hard board can hold a dozen+
		// mines at once, and that many overlapping explosion.wav instances in the same instant would read as
		// noise/audio mush, not a "chain". msSinceLastBang throttles further: at the ripple's fastest cadence
		// (MinRingStepMs=24ms between rings) even one bang per ring would overlap into mush, so a ring only
		// actually bangs once at least MinExplosionGapMs has passed since the last one did -- except the FIRST
		// and LAST ring, which always bang regardless, so the blast is never silent at its start or its finish.
		double msSinceLastBang = double.MaxValue;
		Task lastRingPop = Task.CompletedTask;
		for (int i = 0; i < rings.Count; i++)
		{
			if (i == 0 || i == rings.Count - 1 || msSinceLastBang >= MinExplosionGapMs)
			{
				// Dispatcher.Dispatch, not a direct call: found on a real device, 2026-10-22, right after this
				// per-ring chained sound first shipped -- SoundService.Play's "fire-and-forget" Task only
				// actually yields the UI thread on a genuine async wait; once explosion.wav is cached (true for
				// every ring after the very first one this session), GetBytesAsync's cache hit completes
				// synchronously, so CreatePlayer/Play() end up running INLINE on the UI thread, in the same
				// frame as this ring's Animation.Commit calls below -- exactly the kind of piled-up synchronous
				// work that reads as stutter, not a smooth spread. Dispatching it instead queues that work for
				// its own turn on the message loop, so it doesn't compete with this ring's animation kickoff in
				// the same frame.
				Dispatcher.Dispatch(_sound.PlayExplosion);
				msSinceLastBang = 0;
			}

			// BurstRingCap bounds how many of THIS ring's mines actually get debris particles -- now a visual-
			// density choice more than a raw performance one (SpawnMineBurst only adds cheap Particle records to
			// a GraphicsView-backed ParticleField, see that class's own comment, not one native view per spark
			// any more), but a dozen+ simultaneous bursts on a single wide Hard-board ring would still read as
			// visual noise rather than a readable blast, so every mine still gets its color-swap/pop either way,
			// just not every mine gets its own spark burst on a crowded ring.
			int burstBudget = BurstRingCap;
			var tasks = new List<Task>(rings[i].Count);
			foreach ((Border border, Label label, int _, int _) in rings[i])
			{
				border.BackgroundColor = mineColor;
				border.Stroke = revealedStroke;
				border.Shadow = null!;
				label.FontFamily = "OpenSansSemibold";
				label.Text = "💣";
				border.Scale = CellPopStartScale;
				border.Opacity = 0;
				tasks.Add(AnimateCellPopAsync(border));
				if (burstBudget > 0) { SpawnMineBurst(border); burstBudget--; } // debris scatter at this exact cell, timed with its own ring (see method comment)
			}
			lastRingPop = Task.WhenAll(tasks);
			if (i < rings.Count - 1)
			{
				await Task.Delay(stepDelay);
				msSinceLastBang += stepDelay;
			}
		}
		await lastRingPop; // the final ring's own pop must actually finish before the loss modal covers it

		HandleGameEndIfNeeded();
		SaveProgressIfPlaying();
	}

	// MaxRingStepMs trimmed from 90 to 70 (owner's report, 2026-10-22: the ripple itself read as "stiff/
	// stepped" rather than a continuous spreading wave) -- a FEW-ring loss (e.g. a small tight cluster of
	// mines) previously used the largest per-ring gap, which is exactly the case where each ring's 140ms pop
	// had the least overlap with the next ring's, reading as separate discrete pops instead of one wave. A
	// lower ceiling keeps more overlap between consecutive rings' own pop animations in every case.
	private const int MaxRippleTotalMs = 900;
	private const int MinRingStepMs = 24;
	private const int MaxRingStepMs = 70;
	private const int MinExplosionGapMs = 60;
	private const int BurstRingCap = 6;

	// ===== Particle effects (shared by mine-burst debris + win confetti) =====

	// GraphicsView + a single shared ticker per effect, NOT one native BoxView + one Animation.Commit per
	// particle (the original version of both effects) -- found via the owner's own follow-up research,
	// 2026-10-22: N native Android views each paying their own layout/measure/invalidate/composite cost, plus
	// N independent Animation tickers, is a well-known MAUI anti-pattern for many-small-moving-shapes effects
	// like this, and was a real remaining contributor to the ripple's reported jank even after the
	// Dispatcher.Dispatch sound fix. A GraphicsView is ONE native view; ParticleField below drives every active
	// particle's position/rotation/alpha from a single IDispatcherTimer tick and a single Invalidate() call,
	// and ParticleDrawable.Draw issues one cheap ICanvas fill call per particle in that one native view's own
	// draw pass -- same visual motion as before (positions are still computed via the exact same Easing
	// objects/curves the old per-particle Animations used), just without a view and a ticker per particle.
	// SkiaSharp was deliberately NOT added for this: GraphicsView already ships with Microsoft.Maui.Controls
	// (zero new dependency) and is documented as generally more performant than native controls for exactly
	// this kind of shape-drawing workload; SkiaSharp.Views.Maui.Controls' only line that explicitly declares
	// net10.0-android support is still a preview build, not worth pulling into an app that has already hit
	// real net10-specific package-compat friction this session (see AdsService.LevelPlay's own NU1608 pins).
	private sealed class Particle
	{
		public required double StartX, StartY, DX, DY, Size, LifeMs;
		public required Color Color;
		public double SpawnAtMs; // relative delay when added via ParticleField.Add; converted to absolute sim time there
		public double RotationStart, RotationDelta;
		// 0 = fades linearly across the WHOLE flight (debris); 0.6 = stays fully opaque until 60% of the way
		// through, then fades over the remainder (confetti) -- matches each effect's original Animation.Add
		// start-time fraction exactly.
		public double FadeStartT;
		public Easing XEasing = Easing.Linear;
		public Easing YEasing = Easing.Linear;
		public bool IsCircle = true;
	}

	private sealed class ParticleDrawable : IDrawable
	{
		public List<Particle> Particles = [];
		public double CurrentMs;

		public void Draw(ICanvas canvas, RectF dirtyRect)
		{
			foreach (Particle p in Particles)
			{
				double age = CurrentMs - p.SpawnAtMs;
				if (age < 0) continue; // not born yet (confetti's own randomized start stagger)
				double t = Math.Clamp(age / p.LifeMs, 0, 1);

				double alpha = t <= p.FadeStartT ? 1 : 1 - (t - p.FadeStartT) / Math.Max(0.0001, 1 - p.FadeStartT);
				if (alpha <= 0) continue;

				float x = (float)(p.StartX + p.DX * p.XEasing.Ease(t));
				float y = (float)(p.StartY + p.DY * p.YEasing.Ease(t));
				float half = (float)(p.Size / 2);

				canvas.FillColor = p.Color.WithAlpha((float)alpha);
				canvas.SaveState();
				if (p.RotationDelta != 0) canvas.Rotate((float)(p.RotationStart + p.RotationDelta * t), x, y);
				if (p.IsCircle) canvas.FillCircle(x, y, half);
				else canvas.FillRoundedRectangle(x - half, y - half, (float)p.Size, (float)p.Size, half * 0.3f);
				canvas.RestoreState();
			}
		}
	}

	// Owns the GraphicsView + its particle list + the single ticker that advances them -- it only runs while
	// at least one particle is alive, so an idle GamePage (the common case) costs nothing.
	//
	// Drives that ticker with the SAME Animation.Commit mechanism AnimateCellPopAsync/AnimateConfettiPieceAsync
	// already use elsewhere in this file (proven stable here at real scale -- up to dozens of concurrent
	// Animation.Commit calls per ring, every round so far), not a raw IDispatcherTimer. A first version of this
	// class used IDispatcherTimer at a 16ms interval and caused an actual ANR ("Dò Mìn không phản hồi") on a
	// real device during a dense Hard-board loss -- found by the owner's own device test, 2026-10-22. Whatever
	// the exact mechanism behind that (IDispatcherTimer specifically, or the unbounded particle count below, or
	// both), switching to the battle-tested ticker already carrying every other animation in this file, PLUS a
	// hard particle cap as a second, independent safety net, is the lower-risk fix on both counts at once.
	private sealed class ParticleField
	{
		// Bounds Draw()'s own per-frame cost regardless of how many rings/mines are popping close together --
		// a plain ICanvas fill call is far cheaper than a native view, but still not free at a few hundred per
		// frame, and this is deliberately generous rather than tight (confetti alone can have ~18 alive for
		// over a second; this only matters as a ceiling for a worst-case pileup, not normal operation).
		private const int MaxActiveParticles = 150;
		private const string TickerName = "ParticleField";
		// Generous re-armed slice, not a hard runtime limit: longer than any single particle's own LifeMs
		// (debris 300ms; confetti up to ~2050ms including its own random start stagger) -- if particles are
		// still alive when a slice's own Animation finishes, its `finished` callback just starts another one.

		private const uint RunSliceMs = 2500;

		private readonly GraphicsView _view;
		private readonly List<Particle> _particles = [];
		private readonly ParticleDrawable _drawable = new();
		private bool _running;
		private double _simMs;

		public ParticleField(GraphicsView view)
		{
			_view = view;
			_view.Drawable = _drawable;
		}

		public void Add(Particle particle)
		{
			if (_particles.Count >= MaxActiveParticles) return; // see MaxActiveParticles' own comment
			particle.SpawnAtMs += _simMs;
			_particles.Add(particle);
			_drawable.Particles = _particles;
			if (!_running) Run();
		}

		public void Clear()
		{
			_particles.Clear();
			_running = false;
			_view.AbortAnimation(TickerName);
			_view.Invalidate();
		}

		private void Run()
		{
			_running = true;
			double startMs = _simMs;
			var animation = new Animation(v =>
			{
				_simMs = startMs + v;
				_particles.RemoveAll(p => _simMs - p.SpawnAtMs >= p.LifeMs);
				_drawable.CurrentMs = _simMs;
				_view.Invalidate();
			}, 0, RunSliceMs, Easing.Linear);
			animation.Commit(_view, TickerName, length: RunSliceMs, easing: Easing.Linear, finished: (_, _) =>
			{
				if (_particles.Count > 0) Run(); // a particle added near the very end of a slice can still outlive it -- keep ticking
				else _running = false;
			});
		}
	}

	// ===== Mine burst (per-cell debris scatter) =====

	// A handful of small particles per exploding mine, flung outward from that mine's own screen position and
	// faded over a short flight -- the ring-by-ring spread (above) already shows WHERE the blast is spreading,
	// this is what makes any one cell itself read as "bursting" rather than just flipping color (owner's
	// follow-up, 2026-10-22).
	// BurstParticlesPerMine/size/distance bumped up 2026-10-22 (owner's device test: "hiệu ứng mảnh vỡ nhỏ
	// quá" -- the original 4-6dp particles barely flying 14-26dp read as a faint flicker, not an actual burst,
	// at normal phone viewing distance). Bigger particles need more travel distance too, or they just look
	// like a stationary blob next to the cell instead of something flung outward from it.
	private const int BurstParticlesPerMine = 5;
	private const double BurstDurationMs = 300;

	private void SpawnMineBurst(Border cellBorder)
	{
		if (cellBorder.Width <= 0) return; // not actually laid out yet -- shouldn't happen mid-ripple, defensive only

		// BoardHost is centered inside BoardArea (BuildEmptyBoard/XAML), and MineBurstLayer is BoardArea's OWN
		// direct child, same as BoardHost -- so a cell's position relative to MineBurstLayer's coordinate space
		// is BoardHost's own offset within BoardArea plus the cell's offset within BoardHost.
		double centerX = BoardHost.Bounds.X + cellBorder.Bounds.X + cellBorder.Width / 2;
		double centerY = BoardHost.Bounds.Y + cellBorder.Bounds.Y + cellBorder.Height / 2;

		Color[] colors =
		[
			(Color)Application.Current!.Resources["CellMine"],
			(Color)Application.Current!.Resources["Danger"],
			(Color)Application.Current!.Resources["Accent"], // spark-colored, not just debris-red -- reads more like a burst than a single flat color would
		];

		Random rng = Random.Shared;
		double baseAngle = rng.NextDouble() * 360;
		for (int i = 0; i < BurstParticlesPerMine; i++)
		{
			double angle = baseAngle + i * (360.0 / BurstParticlesPerMine) + (rng.NextDouble() - 0.5) * 24; // evenly spread with a little jitter, not a perfectly mechanical fan
			double radians = angle * Math.PI / 180;
			double distance = rng.Next(26, 46);

			_mineBurstField.Add(new Particle
			{
				StartX = centerX,
				StartY = centerY,
				DX = Math.Cos(radians) * distance,
				DY = Math.Sin(radians) * distance,
				Size = rng.Next(9, 16),
				Color = colors[rng.Next(colors.Length)],
				LifeMs = BurstDurationMs,
				FadeStartT = 0, // fades the whole flight, same as the Opacity 1->0 the old per-particle Animation used
				XEasing = Easing.CubicOut,
				YEasing = Easing.CubicOut,
				IsCircle = true,
			});
		}
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

	// Round-end state: the engine logic already freezes the board on its own (OnCellPrimaryTap/
	// OnCellLongPress bail out once _engine.Status != Playing) -- ResultBanner is a full-page modal from
	// here (owner's direction, 2026-10-21), so it covers/dims the toolbar's Hint/Restart underneath without
	// needing to separately hide them.
	private void ShowResultBanner() => ResultBanner.IsVisible = true;
	// Also tears down any particles left over from a previous round (both fields normally empty themselves out
	// on their own -- ParticleField.Tick stops its own timer once its particle list is empty -- but a "Chơi
	// lại"/"Đóng" mashed mid-flight could otherwise leave either field's timer ticking, and now-orphaned,
	// against a board that has already moved on to a new round).
	private void ShowPlayingControls()
	{
		ResultBanner.IsVisible = false;
		_confettiField.Clear();
		_mineBurstField.Clear();
	}

	private void HandleGameEndIfNeeded()
	{
		if (_engine is not { } engine || engine.Status == GameStatus.Playing) return;

		StopTimer();
		_saves.Clear(); // a finished game is never resumable
		_ads.RegisterGameEnd();

		if (engine.Status == GameStatus.Won)
		{
			_sound.PlayWin();
			// Reset explicitly every time, not just set once in XAML: this same Border/Label pair is reused
			// for a loss too (see the XAML comment on ResultCard), so a loss round must not leave its
			// Danger-tinted card/icon/title behind for the very next win.
			ResultCard.BackgroundColor = (Color)Application.Current!.Resources["SurfaceTonal"];
			ResultCard.Stroke = Colors.Transparent;
			ResultCard.StrokeThickness = 0;
			ResultIconLabel.Text = Icons.CheckCircle;
			ResultIconLabel.TextColor = (Color)Application.Current!.Resources["Success"];
			ResultTitleLabel.TextColor = (Color)Application.Current!.Resources["Success"];
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
				if (_isDaily) _dailyProgress.MarkCompleted(_dailyDate ?? DateOnly.FromDateTime(DateTime.UtcNow));
				StarsLabel.IsVisible = false;
				ResultDetailLabel.Text = isRecord
					? $"Kỷ lục mới: {_elapsedSeconds / 60}:{_elapsedSeconds % 60:00}"
					: $"Thời gian: {_elapsedSeconds / 60}:{_elapsedSeconds % 60:00}";
			}
		}
		else
		{
			// PlayExplosion() is no longer called here -- see AnimateMineExplosionRippleAsync's own comment:
			// it now fires once per ring as the ripple itself plays out (a chained series of bangs), not once
			// after HandleGameEndIfNeeded runs at the very end, well after the ripple has already finished.
			if (AppPreferences.VibrationEnabled && Vibration.Default.IsSupported) Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(300));
			// Loss redesign (2026-10-22, owner's direction): a tinted-red card + stroke + a frown-face icon,
			// not the same plain SurfaceTonal card + gray text the win popup uses -- a loss must read as a
			// failure at a glance, not just via its title text.
			ResultCard.BackgroundColor = (Color)Application.Current!.Resources["DangerSoft"];
			ResultCard.Stroke = (Color)Application.Current!.Resources["Danger"];
			ResultCard.StrokeThickness = 2;
			ResultIconLabel.Text = Icons.MoodBad;
			ResultIconLabel.TextColor = (Color)Application.Current!.Resources["Danger"];
			ResultTitleLabel.TextColor = (Color)Application.Current!.Resources["OffBlack"];
			ResultTitleLabel.Text = "Trúng mìn rồi!";
			ResultDetailLabel.Text = $"Thời gian: {_elapsedSeconds / 60}:{_elapsedSeconds % 60:00}";
			StarsLabel.IsVisible = false;
		}

		ShowResultBanner();
		// Win-only, and only once the card above is actually showing: a modest confetti scatter over/around
		// the result card (owner's direction, 2026-10-22). Loss gets no celebratory motion at all -- the mine
		// ripple (AnimateMineExplosionRippleAsync) already carried that round's "something happened" beat.
		if (engine.Status == GameStatus.Won) StartWinCelebration();
	}

	// ===== Win celebration (confetti) =====

	// Drawn through the same GraphicsView-backed ParticleField as the mine-burst debris above (see that
	// class's own comment) -- originally one native BoxView + one Animation.Commit per piece, replaced
	// 2026-10-22 for the same reason. Kept deliberately small (ConfettiPieceCount); this runs on real
	// mid-range Android phones, not just an emulator.
	private const int ConfettiPieceCount = 18;

	private void StartWinCelebration()
	{
		// The page's own measured size, not ConfettiLayer's/ResultBanner's own -- ResultBanner normally sits
		// IsVisible="False" up until ShowResultBanner (just called above) flips it, and an Android view that
		// was never visible can still report 0 for its own Width/Height for a frame or two after that flip. This page
		// itself has been on-screen and fully measured since well before any round could end, so its Width/
		// Height are already reliable the instant this runs.
		double width = Width > 0 ? Width : 360;
		double height = Height > 0 ? Height : 640;

		Color[] colors =
		[
			(Color)Application.Current!.Resources["Primary"],
			(Color)Application.Current!.Resources["Accent"],
			(Color)Application.Current!.Resources["Success"],
			(Color)Application.Current!.Resources["Number1"],
			(Color)Application.Current!.Resources["Number3"],
			(Color)Application.Current!.Resources["Number6"],
		];

		Random rng = Random.Shared;
		for (int i = 0; i < ConfettiPieceCount; i++)
		{
			double size = rng.Next(6, 12);
			double startX = rng.NextDouble() * Math.Max(1, width - size);
			double startY = -rng.Next(10, 140); // staggered just above the top edge, not a single flat starting row
			double fallDistance = height - startY + 120; // past the bottom edge, so nothing visibly stops mid-air
			double rotationStart = rng.Next(0, 360);

			_confettiField.Add(new Particle
			{
				StartX = startX,
				StartY = startY,
				DX = (rng.NextDouble() - 0.5) * 90, // horizontal drift
				DY = fallDistance,
				Size = size,
				Color = colors[rng.Next(colors.Length)],
				LifeMs = rng.Next(1100, 1700),
				SpawnAtMs = rng.Next(0, 350), // staggered start, same as the old per-piece delayMs
				RotationStart = rotationStart,
				RotationDelta = 180,
				// Fades out only over the back 40% of the fall, so a piece stays fully visible while still high
				// on screen and just dissolves near the bottom, instead of visibly fading the whole way down.
				FadeStartT = 0.6,
				XEasing = Easing.Linear, // drift is a straight line, not eased
				YEasing = Easing.CubicIn, // fall accelerates, same curve the old per-piece Animation used
				IsCircle = rng.Next(2) == 0, // mix of round/square-ish pieces reads more like confetti than uniform shapes
			});
		}
	}

	// ===== Toolbar =====

	private void OnHintClicked(object? sender, EventArgs e)
	{
		if (_engine is not { Status: GameStatus.Playing } engine || _cellBorders == null) return;
		(int X, int Y)? hint = engine.FindHint();
		if (hint is not { } cell)
		{
			// Silent no-op otherwise read as "the button is broken" (owner's report, 2026-10-21): FindHint
			// legitimately has nothing provable yet on plenty of real boards (e.g. right after the first
			// reveal, before enough numbers are exposed for any deduction) -- that is a normal outcome, not
			// an error, so it gets its own distinct feedback rather than none at all.
			_ = Toast.Make("Chưa có gợi ý nào lúc này", ToastDuration.Short).Show();
			return;
		}

		Border border = _cellBorders[cell.X, cell.Y];
		border.Stroke = (Color)Application.Current!.Resources["Accent"];
		border.StrokeThickness = 3;
		_sound.PlayHint();
	}

	private async void OnRestartClicked(object? sender, EventArgs e)
	{
		if (!_nav.TryEnter(sender as View)) return;
		try
		{
			bool confirm = await DisplayAlertAsync("Chơi lại?", "Bàn hiện tại sẽ mất.", "Chơi lại", "Huỷ");
			if (confirm) RestartBoard();
		}
		finally { _nav.Exit(sender as View); }
	}

	// Always today's live preset -- a board resumed from a save (ResumeFrom) left _difficulty set to that
	// save's own, possibly now-stale, dimensions, and "Chơi lại" must not perpetuate those.
	private void RestartBoard()
	{
		_difficulty = CurrentPresetDifficulty();
		StartNewBoard();
	}

	private async void OnBackClicked(object? sender, EventArgs e)
	{
		if (!_nav.TryEnter(sender as View)) return;
		try { await ConfirmAndLeaveAsync(); }
		finally { _nav.Exit(sender as View); }
	}

	// ===== Game-end modal (ResultBanner) =====

	// The round is already over here (ResultBanner only shows once Won/Lost), so unlike OnRestartClicked
	// above there is nothing in-progress to confirm losing -- straight into a fresh board.
	private void OnResultRestartClicked(object? sender, EventArgs e)
	{
		ResultBanner.IsVisible = false;
		RestartBoard();
	}

	// Same reasoning: IsMidGame() is already false once the round has ended, so ConfirmAndLeaveAsync's own
	// confirm dialog never shows here -- this just stops the timer (already stopped) and navigates out.
	private async void OnResultBackClicked(object? sender, EventArgs e)
	{
		if (!_nav.TryEnter(sender as View)) return;
		try { await ConfirmAndLeaveAsync(); }
		finally { _nav.Exit(sender as View); }
	}

	// Just dismisses the modal -- the board underneath is already frozen in its finished state (mines
	// revealed on a loss, RefreshAllCells already painted that), nothing else to do.
	private void OnResultCloseClicked(object? sender, EventArgs e) => ResultBanner.IsVisible = false;

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
		// Daily is reached via Home -> DailyCalendarPage -> GamePage (two hops) since the calendar redesign
		// (owner's direction, 2026-10-20), so a plain ".." pop here only returns to the calendar, not Home --
		// leaving the player unable to see the "Tiếp tục" resume card this very dialog just promised them
		// ("bạn có thể vào lại chơi tiếp từ trang chủ"). Every other mode is still reached in one hop (Home
		// for Easy/Medium/Hard/Resume, LevelsPage for Tutorial), where ".." already lands in the right place.
		if (_isDaily) await Shell.Current.GoToAsync("//HomePage");
		else await Shell.Current.GoToAsync("..");
	}
}
