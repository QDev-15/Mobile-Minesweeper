namespace Minesweeper.Views;

/// <summary>Glyphs of the Material Icons font (Resources/Fonts/MaterialIcons-Regular.ttf, Apache-2.0),
/// registered as the "Icons" font family in MauiProgram.cs. Code points verified against Google's own
/// MaterialIcons-Regular.codepoints reference file (same pattern as PdfReader.Views.Icons) — "trắng sữa"
/// redesign 2026-10-19 replaces the emoji/Unicode-symbol glyphs used across every page with these.
///
/// Every value here MUST be written as a \uXXXX escape, never as a literal pasted glyph character: the
/// Material Icons codepoints live in the Private Use Area, which renders as nothing (zero width, no visible
/// glyph) in a terminal, a diff, or most text views. 9 of these constants were accidentally overwritten with
/// empty "" strings on 2026-10-20 — invisible both before AND after the mistake, so nothing looked wrong
/// anywhere a human (or this assistant) would normally look, until every back/flag/hint/restart/chevron icon
/// in the shipped app silently went blank. Caught only by inspecting the file's raw bytes.</summary>
public static class Icons
{
	public const string ArrowBack = "";          // arrow_back
	public const string Tune = "";               // tune -- stands in for Settings on HomePage's header
	public const string HelpOutline = "";         // help_outline -- stands in for About/Info on HomePage's header
	public const string Flag = "";                // flag
	public const string LightbulbOutline = "";    // lightbulb_outline
	public const string RestartAlt = "";          // restart_alt
	public const string Star = "";                // star
	public const string Share = "";               // share
	public const string ChevronRight = "";        // chevron_right
	public const string StarBorder = "";          // star_border -- not-yet-earned star on a LevelsPage tile
	public const string Lock = "";                // lock -- a not-yet-unlocked LevelsPage tile
	public const string CheckCircle = "";         // check_circle -- a completed day on DailyCalendarPage
	public const string FiberManualRecord = "";   // fiber_manual_record -- a missed day on DailyCalendarPage
	public const string ChevronLeft = "";         // chevron_left -- DailyCalendarPage's previous-month button
	// calendar_month (0xEBCC) looked correct in every offline check (present in the font's cmap, right glyph
	// outline when rasterized directly from the .ttf) but rendered as a wrong/garbled glyph on a real device,
	// 2026-10-20 -- found only by screenshotting it; event (0xE878), a plain calendar-with-dot glyph in the
	// same codepoint neighborhood as other icons already confirmed correct on-device (HelpOutline 0xE8FD,
	// LightbulbOutline 0xE90F), does not have the same problem.
	public const string Event = "";               // event -- opens DailyCalendarPage from HomePage's Daily card
	// Written as a literal \uXXXX escape (not a pasted raw glyph like the constants above) specifically so
	// this one stays greppable/diffable text -- rendered from the actual .ttf with Pillow and visually
	// confirmed as a clean frown-face-in-a-circle before use (2026-10-22, GamePage's loss popup icon), per
	// this file's own header rule.
	public const string MoodBad = "";       // mood_bad -- GamePage ResultBanner loss icon
}
