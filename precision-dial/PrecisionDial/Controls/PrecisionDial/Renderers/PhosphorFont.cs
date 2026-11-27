using System;
using System.IO;
using System.Reflection;
using SkiaSharp;

namespace PrecisionDial.Controls;

/// <summary>
/// Lazy-loaded Phosphor Icons typeface. Uses reflection to reach the Android
/// AssetManager so the code compiles on every target without conditional
/// compilation. On non-Android targets the reflection attempts no-op and the
/// loader falls through to the filesystem path.
/// </summary>
internal static class PhosphorFont
{
    private const string AssetRelativePath = "Assets/Fonts/Phosphor.ttf";

    private static SKTypeface? _typeface;
    private static bool _loadAttempted;
    private static readonly object _gate = new();

    public static SKTypeface? Instance
    {
        get
        {
            if (_typeface is not null) return _typeface;
            if (_loadAttempted) return null;

            lock (_gate)
            {
                if (_loadAttempted) return _typeface;
                _loadAttempted = true;
                try { _typeface = TryLoad(); }
                catch { _typeface = null; }
            }

            return _typeface;
        }
    }

    private static SKTypeface? TryLoad()
    {
        // 1. Android AssetManager via reflection — no compile-time dependency.
        var androidTf = TryLoadFromAndroidAssets();
        if (androidTf is not null) return androidTf;

        // 2. Filesystem (Desktop / iOS / WASM-standalone).
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "Phosphor.ttf");
            if (File.Exists(path))
            {
                using var fs = File.OpenRead(path);
                using var ms = CopyToMemory(fs);
                var tf = SKTypeface.FromStream(ms);
                if (tf is not null) return tf;
            }
        }
        catch { }

        // 3. Manifest resource (pinned via LogicalName in csproj, if present).
        var assembly = typeof(PhosphorFont).Assembly;
        foreach (var candidate in new[] { "Phosphor.ttf", "PrecisionDial.Assets.Fonts.Phosphor.ttf" })
        {
            try
            {
                using var stream = assembly.GetManifestResourceStream(candidate);
                if (stream is null) continue;
                using var ms = CopyToMemory(stream);
                var tf = SKTypeface.FromStream(ms);
                if (tf is not null) return tf;
            }
            catch { }
        }

        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (!name.EndsWith("Phosphor.ttf", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                using var stream = assembly.GetManifestResourceStream(name);
                if (stream is null) continue;
                using var ms = CopyToMemory(stream);
                var tf = SKTypeface.FromStream(ms);
                if (tf is not null) return tf;
            }
            catch { }
        }

        return null;
    }

    /// <summary>
    /// Android: opens the font from the APK assets (Application.Context.Assets). Returns null elsewhere or on failure.
    /// </summary>
    private static SKTypeface? TryLoadFromAndroidAssets()
    {
#if __ANDROID__
        try
        {
            using var stream = Android.App.Application.Context.Assets?.Open(AssetRelativePath);
            if (stream is null) return null;
            using var ms = CopyToMemory(stream);
            return SKTypeface.FromStream(ms);
        }
        catch
        {
            return null;
        }
#else
        return null;
#endif
    }

    private static MemoryStream CopyToMemory(Stream source)
    {
        var ms = new MemoryStream();
        source.CopyTo(ms);
        ms.Position = 0;
        return ms;
    }
}
