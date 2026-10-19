namespace Minesweeper.Views;

/// <summary>Glyphs of the Material Icons font (Resources/Fonts/MaterialIcons-Regular.ttf, Apache-2.0),
/// registered as the "Icons" font family. The 6 reused below are copied byte-for-byte from PdfReader's own
/// Icons.cs (already confirmed correct on a real device there), not retyped -- Material Icons glyphs are
/// private-use-area Unicode code points with no visible representation in a terminal/diff, so retyping one
/// by hand risks silently picking the wrong code point.</summary>
public static class Icons
{
	public const string FontFamily = "Icons";

	public const string Check = "";           // check
	public const string Close = "";           // close
	public const string ChevronRight = "";    // chevron_right
	public const string Settings = "";   // settings
	public const string Info = "";       // info
	public const string ArrowBack = "";     // arrow_back
}
