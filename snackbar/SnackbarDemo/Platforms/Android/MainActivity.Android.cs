using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace SnackbarDemo.Droid;

[Activity(
    MainLauncher = true,
    ConfigurationChanges = global::Uno.UI.ActivityHelper.AllConfigChanges,
    WindowSoftInputMode = SoftInput.AdjustNothing | SoftInput.StateHidden,
    Exported = true
)]
public class MainActivity : Uno.UI.Runtime.Android.ApplicationActivity
{
}
