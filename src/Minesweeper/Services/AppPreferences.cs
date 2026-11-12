namespace Minesweeper.Services;

/// <summary>Sound/vibration on-off switches (owner's direction, 2026-10-22: "trong phần cài đặt thêm mục tắt
/// loa và rung"). Preferences-backed static properties, same pattern as App.ThemeOverride -- both default to
/// on, matching the app's existing behavior before these switches existed.</summary>
public static class AppPreferences
{
    private const string SoundEnabledKey = "pref_sound_enabled";
    private const string VibrationEnabledKey = "pref_vibration_enabled";

    // "_v1": bumping this key later re-prompts everyone with TermsGatePage if its content ever changes
    // meaningfully (added 2026-11-12 alongside the startup terms/ads-disclosure gate).
    private const string TermsAcceptedKey = "terms_accepted_v1";

    public static bool SoundEnabled
    {
        get => Preferences.Default.Get(SoundEnabledKey, true);
        set => Preferences.Default.Set(SoundEnabledKey, value);
    }

    public static bool VibrationEnabled
    {
        get => Preferences.Default.Get(VibrationEnabledKey, true);
        set => Preferences.Default.Set(VibrationEnabledKey, value);
    }

    public static bool TermsAccepted
    {
        get => Preferences.Default.Get(TermsAcceptedKey, false);
        set => Preferences.Default.Set(TermsAcceptedKey, value);
    }
}
