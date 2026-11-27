using System;
using Android.App;
using Android.Runtime;
using Uno.UI.Hosting;

namespace PrecisionDial.Droid;

// Uno Platform 7 entry point: the host is built with UnoPlatformHostBuilder (Uno.UI.Runtime.Android.NativeApplication
// replaces Microsoft.UI.Xaml.NativeApplication from Uno 6).
[global::Android.App.ApplicationAttribute(
    Label = "@string/ApplicationName",
    Icon = "@mipmap/icon",
    LargeHeap = true,
    HardwareAccelerated = true,
    Theme = "@style/Theme.App.Starting"
)]
public class Application : Uno.UI.Runtime.Android.NativeApplication
{
    public Application(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    protected override UnoPlatformHost CreateHost() =>
        UnoPlatformHostBuilder.Create()
            .App(() => new App())
            .UseAndroid()
            .Build();
}
