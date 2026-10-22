using Android.App;
using Android.Content.PM;
using Android.OS;

namespace Minesweeper;

// ScreenOrientation.Portrait (owner's direction, 2026-10-21: no landscape, no auto-rotate -- the board
// layout/sizing (GamePage.BuildEmptyBoard) was never designed for a landscape aspect ratio).
[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ScreenOrientation = ScreenOrientation.Portrait, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
}
