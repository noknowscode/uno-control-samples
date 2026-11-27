using System;
using Android.App;
using Android.Runtime;
using Microsoft.UI.Xaml.Media;
using Uno.UI.Hosting;

namespace SnackbarDemo.Droid;

[global::Android.App.ApplicationAttribute(
    Label = "@string/ApplicationName",
    LargeHeap = true,
    HardwareAccelerated = true,
    Theme = "@style/AppTheme"
)]
public class Application : Uno.UI.Runtime.Android.NativeApplication
{
    public Application(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    // Uno 7: Android builds its host here, the counterpart of Main on the other heads.
    protected override UnoPlatformHost CreateHost() =>
        UnoPlatformHostBuilder.Create()
            .App(() => new App())
            .UseAndroid()
            .Build();
}
