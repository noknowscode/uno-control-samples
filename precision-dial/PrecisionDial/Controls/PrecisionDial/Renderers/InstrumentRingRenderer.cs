using System;
using System.IO;
using SkiaSharp;

namespace PrecisionDial.Controls;

/// <summary>
/// The Control Gallery sheet's ring (<see cref="DialArcStyle.Continuous"/>): a 2 px track and a continuous 2 px lit arc
/// at 0.355 × size with an 8 px glow, 44 ticks outside it (every fourth longer and lighter) lit up to the value, and a
/// copper section past the hot threshold. Optionally a value tip at 0.48 × size. Paints are cached; nothing is
/// allocated per frame except the tip string when the value changes.
/// </summary>
internal sealed class InstrumentRingRenderer
{
    private const int TickCount = 44;

    private readonly SKPaint _track = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2, StrokeCap = SKStrokeCap.Round };
    private readonly SKPaint _arc = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2, StrokeCap = SKStrokeCap.Round };
    private readonly SKPaint _glow = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 8, StrokeCap = SKStrokeCap.Round };
    private readonly SKPaint _tick = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1, StrokeCap = SKStrokeCap.Round };
    private readonly SKPaint _text = new() { IsAntialias = true };
    private readonly SKFont _font = new() { Typeface = LoadMono(), Subpixel = true };
    private double _cachedValue = double.NaN;
    private string _cachedText = string.Empty;

    /// <summary>Unlit marks: track, minor and major ticks. Dark-theme values from the sheet; Light passes its own.</summary>
    public SKColor TrackColor { get; set; } = new(0x26, 0x26, 0x28);
    public SKColor MinorTickColor { get; set; } = new(0x33, 0x33, 0x35);
    public SKColor MajorTickColor { get; set; } = new(0x4A, 0x4A, 0x4C);

    public void Draw(SKCanvas canvas, float cx, float cy, float size, float startAngle, float sweep,
        float normalized, float hotNormalized, SKColor accent, SKColor hot, bool showTip, double value)
    {
        var arcR = size * 0.355f;
        var rect = new SKRect(cx - arcR, cy - arcR, cx + arcR, cy + arcR);
        var valueSweep = Math.Clamp(normalized, 0f, 1f) * sweep;
        var hotStart = hotNormalized is >= 0f and <= 1f ? hotNormalized * sweep : float.PositiveInfinity;

        _track.Color = TrackColor;
        DrawArc(canvas, rect, startAngle, sweep, _track);

        var brassSweep = MathF.Min(valueSweep, hotStart);
        if (brassSweep > 0.2f)
        {
            _glow.Color = accent.WithAlpha(41); // 16 %
            DrawArc(canvas, rect, startAngle, brassSweep, _glow);
            _arc.Color = accent;
            DrawArc(canvas, rect, startAngle, brassSweep, _arc);
        }
        if (valueSweep - hotStart > 0.2f)
        {
            _glow.Color = hot.WithAlpha(51); // 20 %
            DrawArc(canvas, rect, startAngle + hotStart, valueSweep - hotStart, _glow);
            _arc.Color = hot;
            DrawArc(canvas, rect, startAngle + hotStart, valueSweep - hotStart, _arc);
        }

        for (int i = 0; i <= TickCount; i++)
        {
            var major = i % 4 == 0;
            var along = sweep * i / TickCount;
            var angle = (startAngle + along) * MathF.PI / 180f;
            var inner = size * (major ? 0.378f : 0.392f);
            var outer = size * (major ? 0.432f : 0.418f);
            var lit = along <= valueSweep + 0.01f && valueSweep > 0.05f;
            _tick.Color = lit ? (along > hotStart ? hot : accent) : major ? MajorTickColor : MinorTickColor;
            var (cos, sin) = (MathF.Cos(angle), MathF.Sin(angle));
            canvas.DrawLine(cx + inner * cos, cy + inner * sin, cx + outer * cos, cy + outer * sin, _tick);
        }

        if (!showTip) return;
        if (value != _cachedValue)
        {
            _cachedValue = value;
            _cachedText = value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        }
        var tipAngle = (startAngle + valueSweep) * MathF.PI / 180f;
        var tipR = size * 0.48f;
        _font.Size = MathF.Max(9f, size * 0.04f);
        _text.Color = valueSweep > hotStart ? hot : accent;
        var tx = cx + tipR * MathF.Cos(tipAngle);
        var ty = cy + tipR * MathF.Sin(tipAngle) + _font.Size * 0.35f;
        canvas.DrawText(_cachedText, tx, ty, SKTextAlign.Center, _font, _text);
    }

    private static void DrawArc(SKCanvas canvas, SKRect rect, float start, float sweep, SKPaint paint)
        => canvas.DrawArc(rect, start, sweep, false, paint);

    private static SKTypeface LoadMono()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "IBMPlexMono-Medium.ttf");
            if (File.Exists(path) && SKTypeface.FromFile(path) is { } typeface) return typeface;
        }
        catch
        {
        }
        return SKTypeface.FromFamilyName("monospace") ?? SKTypeface.Default;
    }
}
