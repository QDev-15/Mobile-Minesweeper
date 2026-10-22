using Microsoft.Maui.Graphics.Platform;
using Minesweeper.Core;

namespace Minesweeper.Views;

// ===== Canvas-based board rendering =====
//
// Replaces the old one-native-View-per-cell board (288 Border+Label objects in an AbsoluteLayout) with a
// single GraphicsView drawing every cell itself each frame. Measured 2026-10-22: even after trimming the
// per-cell Shadow/Stroke/Grid costs down to a few hundred ms of construction time, actually laying out and
// drawing 288 real native Android views in one frame still cost multiple seconds (Davey frame-drop evidence),
// a cost inherent to the view count, not any one property. A GraphicsView is ONE native view -- the same
// approach already used for the mine-burst/confetti particle effects below -- so cell count no longer has a
// per-cell native-view tax at all; drawing 288 rectangles+strings in one Draw() call is comparable in cost to
// drawing a few dozen confetti particles, already proven fast on a real device.
//
// Cell look (owner's direction, 2026-10-22, matching a classic-Minesweeper reference screenshot): square,
// flush/touching cells (no gap, no rounded corners) with a pre-rendered bevel image stamped on top of a flat
// fill for the "3D raised" look on hidden/flagged cells, instead of a runtime Border.Shadow -- "ảnh tạo cảm
// giác 3D" (the image creates the 3D feeling), "nhẹ hơn là vẽ rồi set thuộc tính" (lighter than drawing and
// setting properties per cell). The bevel is a small (64x64) semi-transparent white/black-edged PNG
// (Resources/Raw/cell_bevel.png, generated once via PIL, not shipped by a designer) loaded ONCE and stamped
// via canvas.DrawImage for every hidden/flagged cell -- a cheap raster blit of a shared bitmap, not a fresh
// rounded-rect+stroke+shadow computation per cell. White/black at modest alpha (not opaque) so it still reads
// correctly tinted under whatever the current theme's CellHidden fill color is, keeping full Light/Dark/High-
// Contrast theme support (the fill color itself is still resolved from DynamicResource at draw time) instead
// of baking one fixed color scheme into the image.
public partial class GamePage
{
    private const double PopDurationMs = 140; // matches the old AnimateCellPopAsync length

    private BoardDrawable? _boardDrawable;
    private BoardAnimator? _boardAnimator;
    private double _cellWidth;
    private double _cellHeight;
    private double _cellFontSize;
    private double[,]? _cellAnims; // per-cell pop-start sim time; -1 = settled/no animation

    // Persistent offscreen canvas backing BoardDrawable.StaticLayer -- kept alive for the WHOLE board's
    // lifetime (recreated only in BuildEmptyBoard, on a resize/new game), not a fresh one per move. Own doc
    // comment on RebuildStaticLayerFull/RebuildStaticLayerCells for why: redrawing the full 288-cell board into
    // a BRAND NEW bitmap on every single move (an earlier version of this fix) still cost 700-1100ms once
    // enough cells were already revealed (every already-open number has to be redrawn too, every time) -- found
    // on a real device, 2026-10-22, still tripping Davey warnings on ordinary single taps at a normal human
    // pace, not just under an artificial rapid-tap stress test. Drawing only the cells that actually changed
    // THIS move into the SAME persistent canvas instead makes each move's cost proportional to how many cells
    // it changed (almost always a handful), not the board's total size or how full it already is.
    private PlatformBitmapExportContext? _staticContext;
    private bool _staticLayerNeedsFullDraw;

    // Press-feedback (owner's direction, 2026-10-20, carried over from the old per-cell TouchBehavior's
    // PressedBackgroundColor): a finger landing on a cell must visibly react the instant it lands, before
    // Reveal/ToggleFlag even runs, or touch reads as unresponsive on a slow device.
    private (int X, int Y)? _pressedCell;

    private void InitBoardCanvas()
    {
        _boardDrawable = new BoardDrawable
        {
            ResolveColor = static key => (Color)Application.Current!.Resources[key],
            ResolveNumberColor = NumberColor,
        };
        BoardCanvas.Drawable = _boardDrawable;
        // Plain events directly on GraphicsView (confirmed by inspecting Microsoft.Maui.Controls.dll's own
        // metadata directly, 2026-10-22, same technique as this session's earlier GestureManager byte-
        // inspection finds) -- distinct from IGraphicsView's own same-named METHODS, which GraphicsView
        // implements explicitly for its platform handler to call into; no "EnableTouchEvents" toggle exists
        // to set first on this MAUI build (10.0.90).
        BoardCanvas.StartInteraction += OnBoardStartInteraction;
        BoardCanvas.EndInteraction += OnBoardEndInteraction;
        BoardCanvas.CancelInteraction += OnBoardCancelInteraction;
        _boardAnimator = new BoardAnimator(BoardCanvas, ms =>
        {
            _boardDrawable.CurrentMs = ms;
            BoardCanvas.Invalidate();
        });
        _ = LoadBevelTileAsync();
    }

    // Fire-and-forget from the constructor: FileSystem.OpenAppPackageFileAsync is async and the constructor
    // can't await it. A frame or two of hidden cells drawn without the bevel overlay before this completes is
    // a harmless, invisible-in-practice cosmetic gap -- BoardLoadingOverlay covers the board for far longer
    // than this load takes anyway.
    private async Task LoadBevelTileAsync()
    {
        try
        {
            using Stream stream = await FileSystem.OpenAppPackageFileAsync("cell_bevel.png");
            _boardDrawable!.BevelTile = PlatformImage.FromStream(stream);
            BoardCanvas.Invalidate();
        }
        catch
        {
            // Missing/corrupt asset: cells just render flat (solid fill, no bevel) instead of crashing --
            // still fully playable, just loses the raised-tile look.
        }
    }

    // ===== Board setup (canvas) =====

    private void BuildEmptyBoard()
    {
        // Width fills BoardArea edge to edge (owner's direction, 2026-10-22: "full width 2 bên không cần để
        // margin") -- no horizontal margin at all. Height still keeps BoardAreaMargin top/bottom for breathing
        // room under the toolbar. Cell width and height are sized INDEPENDENTLY (not a single square cellSize)
        // because this grid is far taller than it is wide (12x24): sizing purely by width would make the board
        // taller than the screen, and sizing purely by height (the old behavior) left visible empty space on
        // both sides instead of reaching full width. A slightly non-square cell -- still clamped individually
        // so neither axis can collapse to 0 or balloon past BoardCellMaxSize -- satisfies both "fills full
        // width" and the earlier "board must never exceed the screen" rule at once.
        double availableWidth = Math.Max(1, BoardArea.Width);
        double availableHeight = Math.Max(1, BoardArea.Height - BoardAreaMargin * 2);
        double cellWidth = Math.Clamp(availableWidth / _difficulty.Width, BoardCellMinSize, BoardCellMaxSize);
        double cellHeight = Math.Clamp(availableHeight / _difficulty.Height, BoardCellMinSize, BoardCellMaxSize);
        double fontSize = Math.Clamp(Math.Min(cellWidth, cellHeight) * 0.42, 9, 17);

        _cellWidth = cellWidth;
        _cellHeight = cellHeight;
        _cellFontSize = fontSize;

        _cellAnims = new double[_difficulty.Width, _difficulty.Height];
        for (int x = 0; x < _difficulty.Width; x++)
            for (int y = 0; y < _difficulty.Height; y++)
                _cellAnims[x, y] = -1;
        _priorCellState = new CellState?[_difficulty.Width, _difficulty.Height];
        _pressedCell = null;

        BoardCanvas.WidthRequest = _difficulty.Width * cellWidth;
        BoardCanvas.HeightRequest = _difficulty.Height * cellHeight;

        BoardDrawable d = _boardDrawable!;
        d.Width = _difficulty.Width;
        d.Height = _difficulty.Height;
        d.CellWidth = cellWidth;
        d.CellHeight = cellHeight;
        d.FontSize = fontSize;
        d.Engine = null; // StartNewBoard/GenerateBoardAsync assigns the real one moments later
        d.AnimStart = _cellAnims;
        d.HintCell = null;
        d.PressedCell = null;
        d.DeferredMines = null;
        d.StaticLayer = null;
        d.AlwaysLiveCells = null;

        // A fresh persistent canvas for this (possibly resized) board -- see the field's own comment for why
        // it is created once here and drawn into incrementally after, not recreated per move. The very first
        // RefreshAllCells call after this still has to draw the whole thing once (nothing exists in the new
        // canvas yet); every one after that only draws whatever actually changed.
        _staticContext?.Dispose();
        int pxWidth = (int)Math.Ceiling(_difficulty.Width * cellWidth);
        int pxHeight = (int)Math.Ceiling(_difficulty.Height * cellHeight);
        _staticContext = pxWidth > 0 && pxHeight > 0 ? new PlatformBitmapExportContext(pxWidth, pxHeight, 1f) : null;
        _staticLayerNeedsFullDraw = true;

        BoardCanvas.Invalidate();
    }

    // Renders every cell's RESTING look into the persistent _staticContext, cached as BoardDrawable.StaticLayer
    // so Draw() can blit it with one cheap DrawImage call instead of re-running all 288 cells' fill/stroke/
    // text/image draws on every single animation tick (owner's report, 2026-10-22: touching an already-
    // revealed cell -- i.e. a chord, however few cells it actually reveals -- and the mine-explosion ripple
    // both felt laggy; Draw() was unconditionally redrawing the WHOLE board on every frame of BoardAnimator's
    // ticker). Only called once, for the first RefreshAllCells after a fresh BuildEmptyBoard -- every move
    // after that uses RebuildStaticLayerCells instead, which draws only what actually changed; redrawing the
    // whole board from scratch on EVERY move (an earlier version of this fix) still cost 700-1100ms once
    // enough cells were already revealed, since every already-open number had to be redrawn every single time
    // too, found on a real device, 2026-10-22, still tripping Davey warnings at a normal human tap pace.
    private void RebuildStaticLayerFull()
    {
        if (_boardDrawable?.Engine == null || _staticContext == null) return;
        _boardDrawable.DrawAllCellContentForStaticLayer(_staticContext.Canvas);
        _boardDrawable.StaticLayer = _staticContext.Image;
    }

    // Draws ONLY the given cells' resting look into the persistent _staticContext, leaving every pixel outside
    // those cells' rects untouched -- each cell's own draw always starts with an opaque FillRectangle, so this
    // cleanly overwrites just that cell's old look with its new one, no separate clear step needed. See
    // RebuildStaticLayerFull's own comment for why this incremental draw exists at all.
    private void RebuildStaticLayerCells(IEnumerable<(int X, int Y)> cells)
    {
        if (_boardDrawable?.Engine == null || _staticContext == null) return;
        _boardDrawable.DrawCellsContentForStaticLayer(_staticContext.Canvas, cells);
        _boardDrawable.StaticLayer = _staticContext.Image;
    }

    // ===== Touch input (canvas) =====
    //
    // GraphicsView's own touch-interaction events, not CommunityToolkit's TouchBehavior: a single shared
    // behavior would still need per-touch coordinate-to-cell math same as this, and there is now exactly ONE of
    // these (not 288), so the prior
    // "TouchBehavior owns the touch stream, a second gesture recognizer on the same view goes dead" conflict
    // (GamePage's own git history, 2026-10-17) does not apply -- there is nothing else on this view to compete
    // with.
    private const int LongPressThresholdMs = 450;
    private const double TapMoveToleranceDp = 12;

    private PointF _touchDownPoint;
    private bool _longPressFired;
    private CancellationTokenSource? _longPressCts;

    private void OnBoardStartInteraction(object? sender, TouchEventArgs e)
    {
        if (e.Touches.Length == 0) return;
        PointF point = e.Touches[0];
        _touchDownPoint = point;
        _longPressFired = false;

        _longPressCts?.Cancel();
        var cts = new CancellationTokenSource();
        _longPressCts = cts;

        if (TryCellAt(point, out int px, out int py))
        {
            _pressedCell = (px, py);
            _boardDrawable!.PressedCell = _pressedCell;
            BoardCanvas.Invalidate();
        }

        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(LongPressThresholdMs), () =>
        {
            if (cts.IsCancellationRequested) return;
            _longPressFired = true;
            if (TryCellAt(point, out int x, out int y)) OnCellLongPress(x, y);
        });
    }

    private void OnBoardEndInteraction(object? sender, TouchEventArgs e)
    {
        _longPressCts?.Cancel();
        ClearPressedCell();

        if (_longPressFired) return; // already handled as a long-press above
        if (e.Touches.Length == 0) return;
        PointF point = e.Touches[0];
        if (Distance(point, _touchDownPoint) > TapMoveToleranceDp) return; // dragged, not a tap
        if (TryCellAt(point, out int x, out int y)) OnCellPrimaryTap(x, y);
    }

    private void OnBoardCancelInteraction(object? sender, EventArgs e)
    {
        _longPressCts?.Cancel();
        ClearPressedCell();
    }

    private void ClearPressedCell()
    {
        if (_pressedCell == null) return;
        _pressedCell = null;
        _boardDrawable!.PressedCell = null;
        BoardCanvas.Invalidate();
    }

    private static double Distance(PointF a, PointF b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    private bool TryCellAt(PointF point, out int x, out int y)
    {
        x = (int)(point.X / _cellWidth);
        y = (int)(point.Y / _cellHeight);
        if (x >= 0 && x < _difficulty.Width && y >= 0 && y < _difficulty.Height) return true;
        x = y = 0;
        return false;
    }

    // ===== Rendering (canvas) =====
    //
    // deferMineReveal: see the old RefreshAllCells's own comment (still accurate) -- true only for the one
    // call that itself triggers a loss, so AnimateMineExplosionRippleAsync can stage the other mines into an
    // outward blast instead of every mine flashing at once. Returns the deferred mines so that method can mark
    // each ring "live" (remove from DeferredMines) as it fires.
    private List<(int X, int Y)> RefreshAllCells(bool deferMineReveal = false)
    {
        if (_engine is not { } engine || _cellAnims == null || _priorCellState == null) return [];

        BoardDrawable d = _boardDrawable!;
        d.Engine = engine;
        d.HintCell = null; // any stale hint from a previous move is cleared the moment a new move repaints

        List<(int X, int Y)> justAppeared = [];
        List<(int X, int Y)> deferredMines = [];
        // Every cell whose state actually changed this pass -- broader than justAppeared/deferredMines (e.g. an
        // un-flag, Flagged back to Hidden, changes what the StaticLayer must show but triggers no pop animation
        // and isn't a mine) -- RebuildStaticLayerCells below only needs to touch these, not the whole board.
        List<(int X, int Y)> changedCells = [];
        HashSet<(int X, int Y)>? deferredSet = deferMineReveal ? [] : null;

        for (int x = 0; x < engine.Width; x++)
        {
            for (int y = 0; y < engine.Height; y++)
            {
                CellState state = engine.StateAt(x, y);
                bool changed = state != _priorCellState[x, y];
                bool isNewlyShown = changed && state is CellState.Revealed or CellState.Flagged;

                if (state == CellState.Revealed && engine.NumberAt(x, y) == -1 && deferMineReveal && isNewlyShown)
                {
                    deferredSet!.Add((x, y));
                    deferredMines.Add((x, y));
                }
                else if (isNewlyShown)
                {
                    justAppeared.Add((x, y));
                }
                if (changed) changedCells.Add((x, y));
                _priorCellState[x, y] = state; // the engine's real state, even for a deferred mine -- see field comment
            }
        }

        d.DeferredMines = deferredSet;

        // See PopAnimationCap's own comment: past the cap, cells just snap straight to rest (AnimStart stays
        // -1, Draw() renders them at scale 1/alpha 1 immediately) instead of animating, trading the flourish
        // for guaranteed smooth frame pacing on a huge flood-fill reveal.
        if (justAppeared.Count > 0 && justAppeared.Count <= PopAnimationCap)
        {
            double now = _boardAnimator!.NowMs;
            foreach ((int x, int y) in justAppeared) _cellAnims[x, y] = now;
            _boardAnimator.Trigger(PopDurationMs + 60);
        }

        MineCountLabel.Text = engine.MinesRemaining.ToString();
        if (_staticLayerNeedsFullDraw) { RebuildStaticLayerFull(); _staticLayerNeedsFullDraw = false; }
        else if (changedCells.Count > 0) RebuildStaticLayerCells(changedCells);
        BoardCanvas.Invalidate();
        return deferredMines;
    }

    private void SpawnMineBurst(int x, int y)
    {
        double centerX = BoardCanvas.Bounds.X + (x + 0.5) * _cellWidth;
        double centerY = BoardCanvas.Bounds.Y + (y + 0.5) * _cellHeight;

        Color[] colors =
        [
            (Color)Application.Current!.Resources["CellMine"],
            (Color)Application.Current!.Resources["Danger"],
            (Color)Application.Current!.Resources["Accent"],
        ];

        Random rng = Random.Shared;
        double baseAngle = rng.NextDouble() * 360;
        for (int i = 0; i < BurstParticlesPerMine; i++)
        {
            double angle = baseAngle + i * (360.0 / BurstParticlesPerMine) + (rng.NextDouble() - 0.5) * 24;
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
                FadeStartT = 0,
                XEasing = Easing.CubicOut,
                YEasing = Easing.CubicOut,
                IsCircle = true,
            });
        }
    }

    // ===== IDrawable =====

    private sealed class BoardDrawable : IDrawable
    {
        public GameEngine? Engine;
        public int Width, Height;
        public double CellWidth, CellHeight;
        public double FontSize;
        public double CurrentMs;
        public double[,]? AnimStart;
        public (int X, int Y)? HintCell;
        public (int X, int Y)? PressedCell;
        public HashSet<(int X, int Y)>? DeferredMines;
        public Microsoft.Maui.Graphics.IImage? BevelTile;
        public Func<string, Color> ResolveColor = null!;
        public Func<int, Color> ResolveNumberColor = null!;

        // Cached render of every cell at its RESTING look (own doc comment on RebuildStaticLayerFull/Cells,
        // GamePage.Board.cs, for why): Draw() blits this with one cheap DrawImage call instead of redrawing all
        // 288 cells on every single animation tick, and only re-draws (live, every frame) the handful of cells actually listed
        // below as needing it right now.
        public Microsoft.Maui.Graphics.IImage? StaticLayer;
        // Cells the mine-explosion ripple is actively staging THIS round -- broader than "currently mid-pop-
        // animation" (own doc comment on AnimateMineExplosionRippleAsync's AlwaysLiveCells assignment) because a
        // still-deferred mine is not animating yet but must still be read live (DeferredMines), not from a
        // StaticLayer snapshot taken before the ripple started.
        public HashSet<(int X, int Y)>? AlwaysLiveCells;

        private static readonly Microsoft.Maui.Graphics.Font TextFont = new("OpenSansSemibold");

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            if (Engine == null || AnimStart == null) return;
            GameEngine engine = Engine;

            Color hiddenColor = ResolveColor("CellHidden");
            Color hiddenPressed = ResolveColor("CellHiddenPressed");
            Color revealedColor = ResolveColor("CellRevealed");
            Color mineColor = ResolveColor("CellMine");
            Color gridLine = ResolveColor("MilkBackground");
            Color accent = ResolveColor("Accent");
            Color primary = ResolveColor("Primary");

            if (StaticLayer != null)
                canvas.DrawImage(StaticLayer, 0, 0, (float)(Width * CellWidth), (float)(Height * CellHeight));

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    bool animating = AnimStart![x, y] >= 0 && (CurrentMs - AnimStart[x, y]) < PopDurationMs;
                    bool isPressed = PressedCell == (x, y);
                    bool alwaysLive = AlwaysLiveCells != null && AlwaysLiveCells.Contains((x, y));
                    if (StaticLayer != null && !animating && !isPressed && !alwaysLive) continue; // StaticLayer already shows this cell correctly
                    DrawCell(canvas, engine, x, y, hiddenColor, hiddenPressed, revealedColor, mineColor, gridLine, accent, primary, ignoreAnimation: false);
                }
            }

            if (HintCell is { X: var hx, Y: var hy })
            {
                float hpx = (float)(hx * CellWidth), hpy = (float)(hy * CellHeight);
                float hw = (float)CellWidth, hh = (float)CellHeight;
                canvas.StrokeColor = accent;
                canvas.StrokeSize = 3;
                canvas.DrawRectangle(hpx + 1.5f, hpy + 1.5f, hw - 3, hh - 3);
            }
        }

        // Draws every cell at its resting look (scale 1, alpha 1, ignoring AnimStart/PressedCell) -- used only
        // to populate StaticLayer via an offscreen PlatformBitmapExportContext (GamePage.Board.cs), never
        // called against the live on-screen canvas directly.
        public void DrawAllCellContentForStaticLayer(ICanvas canvas)
        {
            if (Engine == null) return;
            GameEngine engine = Engine;
            Color hiddenColor = ResolveColor("CellHidden");
            Color hiddenPressed = ResolveColor("CellHiddenPressed");
            Color revealedColor = ResolveColor("CellRevealed");
            Color mineColor = ResolveColor("CellMine");
            Color gridLine = ResolveColor("MilkBackground");
            Color accent = ResolveColor("Accent");
            Color primary = ResolveColor("Primary");

            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    DrawCell(canvas, engine, x, y, hiddenColor, hiddenPressed, revealedColor, mineColor, gridLine, accent, primary, ignoreAnimation: true);
        }

        // Same as DrawAllCellContentForStaticLayer, but only for the given cells -- GamePage.Board.cs's
        // RebuildStaticLayerCells own comment for why this incremental version exists.
        public void DrawCellsContentForStaticLayer(ICanvas canvas, IEnumerable<(int X, int Y)> cells)
        {
            if (Engine == null) return;
            GameEngine engine = Engine;
            Color hiddenColor = ResolveColor("CellHidden");
            Color hiddenPressed = ResolveColor("CellHiddenPressed");
            Color revealedColor = ResolveColor("CellRevealed");
            Color mineColor = ResolveColor("CellMine");
            Color gridLine = ResolveColor("MilkBackground");
            Color accent = ResolveColor("Accent");
            Color primary = ResolveColor("Primary");

            foreach ((int x, int y) in cells)
                DrawCell(canvas, engine, x, y, hiddenColor, hiddenPressed, revealedColor, mineColor, gridLine, accent, primary, ignoreAnimation: true);
        }

        private void DrawCell(ICanvas canvas, GameEngine engine, int x, int y,
            Color hiddenColor, Color hiddenPressed, Color revealedColor, Color mineColor, Color gridLine, Color accent, Color primary,
            bool ignoreAnimation)
        {
            float px = (float)(x * CellWidth), py = (float)(y * CellHeight), w = (float)CellWidth, h = (float)CellHeight;
            CellState state = engine.StateAt(x, y);
            if (DeferredMines != null && DeferredMines.Contains((x, y))) state = CellState.Hidden;

            double animStart = ignoreAnimation ? -1 : AnimStart![x, y];
            float scale = 1, alpha = 1;
            if (animStart >= 0)
            {
                double t = Math.Clamp((CurrentMs - animStart) / PopDurationMs, 0, 1);
                double eased = Easing.CubicOut.Ease(t);
                scale = (float)(CellPopStartScale + (1 - CellPopStartScale) * eased);
                alpha = (float)eased;
            }

            canvas.SaveState();
            canvas.Alpha = alpha;
            if (scale != 1)
            {
                float cx = px + w / 2, cy = py + h / 2;
                canvas.Translate(cx, cy);
                canvas.Scale(scale, scale);
                canvas.Translate(-cx, -cy);
            }

            bool isPressed = !ignoreAnimation && PressedCell == (x, y);

            switch (state)
            {
                case CellState.Hidden:
                    DrawHiddenLike(canvas, px, py, w, h, isPressed ? hiddenPressed : hiddenColor);
                    break;
                case CellState.Flagged:
                    DrawHiddenLike(canvas, px, py, w, h, isPressed ? hiddenPressed : hiddenColor);
                    DrawFlag(canvas, px, py, w, h, primary, accent);
                    break;
                case CellState.Revealed:
                    int number = engine.NumberAt(x, y);
                    if (number == -1)
                    {
                        canvas.FillColor = mineColor;
                        canvas.FillRectangle(px, py, w, h);
                        DrawMine(canvas, px, py, w, h);
                    }
                    else
                    {
                        canvas.FillColor = isPressed ? hiddenPressed : revealedColor;
                        canvas.FillRectangle(px, py, w, h);
                        canvas.StrokeColor = gridLine;
                        canvas.StrokeSize = 1;
                        canvas.DrawRectangle(px, py, w, h);
                        if (number > 0) DrawNumber(canvas, px, py, w, h, number);
                    }
                    break;
            }

            canvas.RestoreState();
        }

        private void DrawHiddenLike(ICanvas canvas, float px, float py, float w, float h, Color fill)
        {
            canvas.FillColor = fill;
            canvas.FillRectangle(px, py, w, h);
            if (BevelTile != null) canvas.DrawImage(BevelTile, px, py, w, h);
        }

        private void DrawNumber(ICanvas canvas, float px, float py, float w, float h, int number)
        {
            canvas.Font = TextFont;
            canvas.FontSize = (float)FontSize;
            canvas.FontColor = ResolveNumberColor(number);
            canvas.DrawString(number.ToString(), px, py, w, h, HorizontalAlignment.Center, VerticalAlignment.Center);
        }

        // Plain vector shapes, not the "Icons" font's flag glyph: Microsoft.Maui.Graphics' ICanvas.DrawString
        // does not resolve a custom MAUI-registered font family the same way Label.FontFamily does on Android
        // (confirmed on a real device, 2026-10-22 -- it silently fell back to a system font that happened to
        // define an unrelated CJK character at that Private Use Area codepoint, the exact "lỗi font" the owner
        // flagged). A hand-drawn pennant has no font dependency at all, so this class of bug cannot recur.
        //
        // A chunky rounded pole + a solid filled pennant (owner's reference image, 2026-10-22), not the earlier
        // thin-stroke version: poleColor (Primary) for the bar, pennantColor (Accent) for the triangle, so it
        // still reads as two-tone against any theme instead of one flat color.
        private static void DrawFlag(ICanvas canvas, float px, float py, float w, float h, Color poleColor, Color pennantColor)
        {
            float poleX = px + w * 0.4f;
            float poleTop = py + h * 0.18f, poleBottom = py + h * 0.82f;
            float poleWidth = Math.Max(2f, Math.Min(w, h) * 0.14f);

            canvas.FillColor = poleColor;
            canvas.FillRoundedRectangle(poleX - poleWidth / 2f, poleTop, poleWidth, poleBottom - poleTop, poleWidth / 2f);

            var pennant = new PathF();
            pennant.MoveTo(poleX + poleWidth * 0.1f, poleTop + h * 0.02f);
            pennant.LineTo(px + w * 0.82f, poleTop + h * 0.16f);
            pennant.LineTo(poleX + poleWidth * 0.1f, poleTop + h * 0.3f);
            pennant.Close();
            canvas.FillColor = pennantColor;
            canvas.FillPath(pennant);
        }

        // Same reasoning as DrawFlag above: a hand-drawn spiked circle instead of the 💣 emoji glyph, which
        // hits the identical ICanvas/custom-font-resolution risk (emoji fallback through Microsoft.Maui.Graphics'
        // Android text path is a different, less reliable mechanism than a Label's own emoji rendering).
        private static void DrawMine(ICanvas canvas, float px, float py, float w, float h)
        {
            float cx = px + w / 2f, cy = py + h / 2f;
            float r = Math.Min(w, h) * 0.26f;
            canvas.StrokeColor = Colors.Black;
            canvas.StrokeSize = Math.Max(1.2f, r * 0.22f);
            for (int i = 0; i < 8; i++)
            {
                double angle = i * Math.PI / 4;
                float dx = (float)Math.Cos(angle), dy = (float)Math.Sin(angle);
                canvas.DrawLine(cx + dx * r * 0.95f, cy + dy * r * 0.95f, cx + dx * r * 1.7f, cy + dy * r * 1.7f);
            }
            canvas.FillColor = Colors.Black;
            canvas.FillCircle(cx, cy, r);
            canvas.FillColor = Colors.White.WithAlpha(0.55f);
            canvas.FillCircle(cx - r * 0.32f, cy - r * 0.32f, r * 0.22f);
        }
    }

    // Drives BoardDrawable's CurrentMs + Invalidate() while any cell has an active pop/explosion animation,
    // same Animation.Commit ticker mechanism as ParticleField below (proven stable at real scale already this
    // session), and idle (zero cost) otherwise.
    private sealed class BoardAnimator
    {
        private const string TickerName = "BoardAnimator";
        private const uint SliceMs = 500;

        private readonly GraphicsView _view;
        private readonly Action<double> _onTick;
        private bool _running;
        private double _simMs;
        private double _activeUntilMs = double.NegativeInfinity;

        public BoardAnimator(GraphicsView view, Action<double> onTick)
        {
            _view = view;
            _onTick = onTick;
        }

        public double NowMs => _simMs;

        public void Trigger(double activeForMs)
        {
            _activeUntilMs = _simMs + activeForMs;
            if (!_running) Run();
        }

        private void Run()
        {
            _running = true;
            double startMs = _simMs;
            var animation = new Animation(v =>
            {
                _simMs = startMs + v;
                _onTick(_simMs);
            }, 0, SliceMs, Easing.Linear);
            animation.Commit(_view, TickerName, length: SliceMs, easing: Easing.Linear, finished: (_, _) =>
            {
                if (_simMs < _activeUntilMs) Run();
                else _running = false;
            });
        }
    }
}
