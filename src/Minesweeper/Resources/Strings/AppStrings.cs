using System.Globalization;
using System.Resources;

namespace Minesweeper.Resources.Strings;

/// <summary>Hand-written instead of the usual Visual Studio "ResXFileCodeGenerator" designer class: that
/// generator is a VS-IDE single-file-generator feature, not an MSBuild/dotnet-CLI one -- `dotnet build` from
/// this session's shell never runs it, so a plain <c>AppStrings.resx</c> alone would compile (SDK-style
/// projects embed .resx automatically) but leave no strongly-typed properties for XAML's
/// <c>{x:Static strings:AppStrings.Key}</c> to bind to. Each property below is a thin, always-fresh lookup
/// through <see cref="ResourceManager"/> against <see cref="CultureInfo.CurrentUICulture"/> -- correct for a
/// fresh page built AFTER a language change (the whole point of <c>AppLanguage.SetOverride</c> rebuilding the
/// Shell) and for any C# code that reads it directly (Toast/DisplayAlert text), which re-evaluates on every
/// call regardless of when the page itself was constructed.
///
/// Base resx (no culture suffix, this file's own data) is Vietnamese -- the app's original/only language
/// before this -- so an unsupported device locale or a missing key in a satellite file always still falls
/// back to a real, correct Vietnamese string instead of a blank or an English default the rest of the app
/// never had.</summary>
public static class AppStrings
{
    private static readonly ResourceManager Rm = new("Minesweeper.Resources.Strings.AppStrings", typeof(AppStrings).Assembly);

    private static string Get(string key) => Rm.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    /// <summary>For the handful of formatted strings (resx value has <c>{0}</c>/<c>{1}</c>... placeholders) --
    /// plain property access wouldn't let a caller supply the arguments, so those use this method instead of a
    /// property. Positional, not named, placeholders: word order/count can legitimately differ per language,
    /// and positional is what <see cref="string.Format(string, object?[])"/> needs for this; the comment on
    /// each formatted key below documents what each {N} is for the next translator.</summary>
    private static string Format(string key, params object?[] args) => string.Format(CultureInfo.CurrentUICulture, Get(key), args);

    // ===== Common / shared across pages =====
    public static string AppName => Get(nameof(AppName));
    public static string Common_Play => Get(nameof(Common_Play));
    public static string Common_PlayAgain => Get(nameof(Common_PlayAgain));
    public static string Common_PlayNow => Get(nameof(Common_PlayNow));
    /// <summary>{0} = date, already formatted by the caller (e.g. "22/10" or locale-appropriate).</summary>
    public static string Common_PlayOnDate(object date) => Format(nameof(Common_PlayOnDate), date);
    public static string Common_Back => Get(nameof(Common_Back));
    public static string Common_Close => Get(nameof(Common_Close));
    public static string Common_Cancel => Get(nameof(Common_Cancel));
    public static string Common_Continue => Get(nameof(Common_Continue));
    public static string Common_Daily => Get(nameof(Common_Daily));
    public static string Common_PrivacyPolicy => Get(nameof(Common_PrivacyPolicy));
    public static string Common_Settings => Get(nameof(Common_Settings));
    /// <summary>{0} = version string, {1} = build string.</summary>
    public static string Common_Version(object version, object build) => Format(nameof(Common_Version), version, build);

    // ===== Difficulty names =====
    public static string Difficulty_Easy => Get(nameof(Difficulty_Easy));
    public static string Difficulty_Medium => Get(nameof(Difficulty_Medium));
    public static string Difficulty_Hard => Get(nameof(Difficulty_Hard));

    // ===== HomePage =====
    public static string Home_ResumeTitle => Get(nameof(Home_ResumeTitle));
    public static string Home_Today => Get(nameof(Home_Today));
    public static string Home_DailyDescription => Get(nameof(Home_DailyDescription));
    public static string Home_Tutorial => Get(nameof(Home_Tutorial));
    public static string Home_NormalPlay => Get(nameof(Home_NormalPlay));
    public static string Home_ExitToast => Get(nameof(Home_ExitToast));
    /// <summary>{0} = unlocked level, {1} = max level, {2} = total stars.</summary>
    public static string Home_TutorialProgress(object unlocked, object max, object stars) => Format(nameof(Home_TutorialProgress), unlocked, max, stars);
    /// <summary>{0} = "{mineCount} mìn"-style size text, {1} = formatted best time.</summary>
    public static string Home_DifficultyBest(object size, object time) => Format(nameof(Home_DifficultyBest), size, time);
    /// <summary>{0} = mine count.</summary>
    public static string Home_MineCount(object mineCount) => Format(nameof(Home_MineCount), mineCount);

    // ===== GamePage =====
    public static string Game_GeneratingBoard => Get(nameof(Game_GeneratingBoard));
    public static string Game_Won => Get(nameof(Game_Won));
    public static string Game_Lost => Get(nameof(Game_Lost));
    /// <summary>{0} = level number, {1} = formatted time.</summary>
    public static string Game_LevelResult(object level, object time) => Format(nameof(Game_LevelResult), level, time);
    /// <summary>{0} = formatted time.</summary>
    public static string Game_NewRecord(object time) => Format(nameof(Game_NewRecord), time);
    /// <summary>{0} = formatted time.</summary>
    public static string Game_TimeResult(object time) => Format(nameof(Game_TimeResult), time);
    public static string Game_NoHintYet => Get(nameof(Game_NoHintYet));
    public static string Game_RestartConfirmTitle => Get(nameof(Game_RestartConfirmTitle));
    public static string Game_RestartConfirmMessage => Get(nameof(Game_RestartConfirmMessage));
    public static string Game_LeaveConfirmTitle => Get(nameof(Game_LeaveConfirmTitle));
    public static string Game_LeaveConfirmMessage => Get(nameof(Game_LeaveConfirmMessage));
    public static string Game_Leave => Get(nameof(Game_Leave));
    public static string Game_Stay => Get(nameof(Game_Stay));

    // ===== AboutPage =====
    public static string About_Title => Get(nameof(About_Title));
    public static string About_Description => Get(nameof(About_Description));
    public static string About_Author => Get(nameof(About_Author));

    // ===== SettingsPage =====
    public static string Settings_ProTier => Get(nameof(Settings_ProTier));
    public static string Settings_BuyPro => Get(nameof(Settings_BuyPro));
    public static string Settings_RestorePurchase => Get(nameof(Settings_RestorePurchase));
    public static string Settings_ProFeature1 => Get(nameof(Settings_ProFeature1));
    public static string Settings_ProFeature2 => Get(nameof(Settings_ProFeature2));
    public static string Settings_ProFeature3 => Get(nameof(Settings_ProFeature3));
    public static string Settings_Appearance => Get(nameof(Settings_Appearance));
    public static string Settings_ThemeSystem => Get(nameof(Settings_ThemeSystem));
    public static string Settings_ThemeLight => Get(nameof(Settings_ThemeLight));
    public static string Settings_ThemeDark => Get(nameof(Settings_ThemeDark));
    public static string Settings_ThemeHighContrast => Get(nameof(Settings_ThemeHighContrast));
    public static string Settings_SoundVibration => Get(nameof(Settings_SoundVibration));
    public static string Settings_Sound => Get(nameof(Settings_Sound));
    public static string Settings_Vibration => Get(nameof(Settings_Vibration));
    public static string Settings_App => Get(nameof(Settings_App));
    public static string Settings_AppInfo => Get(nameof(Settings_AppInfo));
    public static string Settings_Error => Get(nameof(Settings_Error));
    public static string Settings_PurchaseFailed => Get(nameof(Settings_PurchaseFailed));
    public static string Settings_RestoreSuccess => Get(nameof(Settings_RestoreSuccess));
    public static string Settings_RestoreFailed => Get(nameof(Settings_RestoreFailed));
    public static string Settings_RestoreNotFound => Get(nameof(Settings_RestoreNotFound));
    public static string Settings_RestoreTitle => Get(nameof(Settings_RestoreTitle));
    public static string Settings_Language => Get(nameof(Settings_Language));

    // ===== LevelsPage =====
    /// <summary>{0} = unlocked level, {1} = max level, {2} = total stars.</summary>
    public static string Levels_Progress(object unlocked, object max, object stars) => Format(nameof(Levels_Progress), unlocked, max, stars);

    // ===== DailyCalendarPage =====
    public static string Daily_CalendarTitle => Get(nameof(Daily_CalendarTitle));
    public static string Daily_Completed => Get(nameof(Daily_Completed));
    public static string Daily_NotCompleted => Get(nameof(Daily_NotCompleted));

    // ===== Licensing =====
    public static string License_Pro => Get(nameof(License_Pro));
    public static string License_Free => Get(nameof(License_Free));
    public static string License_ConnectRetryError => Get(nameof(License_ConnectRetryError));
    public static string License_ConnectError => Get(nameof(License_ConnectError));

    // ===== PrivacyPolicyPage =====
    /// <summary>{0} = formatted date.</summary>
    public static string Privacy_LastUpdated(object date) => Format(nameof(Privacy_LastUpdated), date);
    public static string Privacy_Overview_Heading => Get(nameof(Privacy_Overview_Heading));
    public static string Privacy_Overview_Body => Get(nameof(Privacy_Overview_Body));
    public static string Privacy_DirectData_Heading => Get(nameof(Privacy_DirectData_Heading));
    public static string Privacy_DirectData_Body => Get(nameof(Privacy_DirectData_Body));
    public static string Privacy_ThirdParty_Heading => Get(nameof(Privacy_ThirdParty_Heading));
    public static string Privacy_ThirdParty_Intro => Get(nameof(Privacy_ThirdParty_Intro));
    public static string Privacy_LevelPlay_Title => Get(nameof(Privacy_LevelPlay_Title));
    public static string Privacy_LevelPlay_Body => Get(nameof(Privacy_LevelPlay_Body));
    public static string Privacy_Billing_Title => Get(nameof(Privacy_Billing_Title));
    public static string Privacy_Billing_Body => Get(nameof(Privacy_Billing_Body));
    public static string Privacy_ThirdParty_Outro => Get(nameof(Privacy_ThirdParty_Outro));
    public static string Privacy_UnityLink => Get(nameof(Privacy_UnityLink));
    public static string Privacy_GoogleLink => Get(nameof(Privacy_GoogleLink));
    public static string Privacy_Permissions_Heading => Get(nameof(Privacy_Permissions_Heading));
    public static string Privacy_Permissions_Body => Get(nameof(Privacy_Permissions_Body));
    public static string Privacy_Children_Heading => Get(nameof(Privacy_Children_Heading));
    public static string Privacy_Children_Body => Get(nameof(Privacy_Children_Body));
    public static string Privacy_DataRetention_Heading => Get(nameof(Privacy_DataRetention_Heading));
    public static string Privacy_DataRetention_Body => Get(nameof(Privacy_DataRetention_Body));
    public static string Privacy_PolicyChanges_Heading => Get(nameof(Privacy_PolicyChanges_Heading));
    public static string Privacy_PolicyChanges_Body => Get(nameof(Privacy_PolicyChanges_Body));
    public static string Privacy_Contact_Heading => Get(nameof(Privacy_Contact_Heading));
    public static string Privacy_Contact_Body => Get(nameof(Privacy_Contact_Body));
}
