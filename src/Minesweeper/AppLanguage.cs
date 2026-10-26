using System.Globalization;

namespace Minesweeper;

/// <summary>Which language the app shows, same shape/pattern as App's own ThemeOverride (Preferences-backed,
/// a static property, applied once at startup and again whenever SettingsPage's picker changes it). Default
/// (no saved override yet) is the device's own current UI language if the app actually supports it, Vietnamese
/// otherwise -- "mỗi quốc gia down app về sẽ được setup đúng language" (owner's request, 2026-10-26): a phone
/// set to Japanese gets Japanese on first launch, with no server-side/store-side "country" detection needed,
/// since CultureInfo.CurrentUICulture already reflects the device's own chosen language.</summary>
public static class AppLanguage
{
    private const string OverrideKey = "LanguageOverrideCode";

    /// <summary>Every language AppStrings.resx actually has a satellite file for, in the order SettingsPage's
    /// picker lists them -- native names only (a French speaker should never need to already read English to
    /// find "Français" in this list). "vi" first: it is the base resx (no culture suffix), not a satellite.</summary>
    public static readonly (string Code, string NativeName)[] Supported =
    [
        ("vi", "Tiếng Việt"),
        ("en", "English"),
        ("zh-Hans", "中文"),
        ("ja", "日本語"),
        ("ko", "한국어"),
        ("es", "Español"),
        ("pt", "Português"),
        ("fr", "Français"),
        ("de", "Deutsch"),
        ("ru", "Русский"),
        ("id", "Bahasa Indonesia"),
    ];

    /// <summary>The language code actually in effect right now: the saved manual override if SettingsPage's
    /// picker has ever been used, otherwise whichever supported language best matches the device's own current
    /// UI language, otherwise Vietnamese (the base resx, always correct, never a blank/missing key).</summary>
    public static string CurrentCode => Preferences.Default.Get(OverrideKey, DetectDeviceDefault());

    private static string DetectDeviceDefault()
    {
        CultureInfo device = CultureInfo.CurrentUICulture;
        // Exact match first (covers "zh-Hans" itself, if a device ever genuinely reports that), then a plain
        // two-letter match (covers the common case: device reports "ja-JP", "pt-BR", "zh-CN"... and this app
        // only ships one variant per language) -- "zh" specifically is pinned to "zh-Hans" (Simplified) below
        // regardless of which Chinese region/script the device reports, since that is the only Chinese variant
        // this app has a translation for; a Traditional-Chinese reader still gets a readable, if not
        // script-perfect, Chinese page instead of silently falling back to Vietnamese.
        foreach ((string code, _) in Supported)
            if (string.Equals(device.Name, code, StringComparison.OrdinalIgnoreCase)) return code;

        string twoLetter = device.TwoLetterISOLanguageName;
        if (string.Equals(twoLetter, "zh", StringComparison.OrdinalIgnoreCase)) return "zh-Hans";
        foreach ((string code, _) in Supported)
            if (string.Equals(code, twoLetter, StringComparison.OrdinalIgnoreCase)) return code;

        return "vi";
    }

    /// <summary>Sets CultureInfo.CurrentUICulture/CurrentCulture (and the per-thread defaults, so a later
    /// background Task.Run still sees the right language too) to CurrentCode -- call once at startup
    /// (App's own constructor, mirroring ApplyColorTheme) and this is all a fresh page's {x:Static
    /// strings:AppStrings.X} bindings or a Toast's AppStrings.X call needs to come out right.</summary>
    public static void Apply()
    {
        var culture = new CultureInfo(CurrentCode);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
    }

    /// <summary>SettingsPage's picker calls this. Persists the choice, applies it immediately (so anything
    /// read from C# from this point on -- including the SAME Settings page's own already-built Labels, which
    /// are NOT {x:Static} and so re-read AppStrings fine without a rebuild -- is already correct), then rebuilds
    /// the whole Shell from scratch: every OTHER already-built page's {x:Static} text only resolves once, at
    /// construction, so there is no cheaper way to make it current than constructing those pages again. Same
    /// "replace the root page" technique already proven for theme changes elsewhere in this app, just applied
    /// to the whole Shell instead of a single ResourceDictionary.</summary>
    public static void SetOverride(string code)
    {
        Preferences.Default.Set(OverrideKey, code);
        Apply();
        if (Application.Current is { } app && app.Windows.Count > 0)
            app.Windows[0].Page = new AppShell();
    }
}
