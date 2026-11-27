using System;
using Android.Content;
using Android.OS;

namespace PrecisionDial.Controls;

public partial class HapticService
{
    private Vibrator? _vibrator;

    public partial void Prepare()
    {
        var context = Android.App.Application.Context;
        _vibrator = OperatingSystem.IsAndroidVersionAtLeast(31)
            ? ((VibratorManager?)context.GetSystemService(Context.VibratorManagerService))?.DefaultVibrator
#pragma warning disable CA1422 // VibratorService is the only way to get the vibrator before Android 12 (API 31)
            : (Vibrator?)context.GetSystemService(Context.VibratorService);
#pragma warning restore CA1422
    }

    private enum Pulse { Tick, Click, HeavyClick }

    public partial void FireDetentClick() => Fire(Pulse.Tick, 5);

    public partial void FireMajorDetentClick() => Fire(Pulse.Click, 10);

    public partial void FireBoundaryStop() => Fire(Pulse.HeavyClick, 30);

    public partial void FireSelectionTick() => FireDetentClick();

    public partial void Release() { _vibrator = null; }

    // Predefined effects from Android 10 (API 29); a one-shot pulse on 8 and 9 (API 26-28); the legacy call before.
    private void Fire(Pulse pulse, long fallbackMilliseconds)
    {
        if (_vibrator is null) return;

        if (OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            var effect = pulse switch
            {
                Pulse.Click => VibrationEffect.EffectClick,
                Pulse.HeavyClick => VibrationEffect.EffectHeavyClick,
                _ => VibrationEffect.EffectTick,
            };
            _vibrator.Vibrate(VibrationEffect.CreatePredefined(effect));
        }
        else if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            _vibrator.Vibrate(VibrationEffect.CreateOneShot(fallbackMilliseconds, VibrationEffect.DefaultAmplitude));
        }
        else
        {
#pragma warning disable CA1422, CS0618 // Vibrate(long) is the only API before Android 8 (API 26)
            _vibrator.Vibrate(fallbackMilliseconds);
#pragma warning restore CA1422, CS0618
        }
    }
}
