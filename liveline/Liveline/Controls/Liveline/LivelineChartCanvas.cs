using Liveline.Models;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;
using Windows.Storage;

namespace Liveline;

/// <summary>Everything one frame of the plot needs, computed by <see cref="LivelineChart"/> on the UI thread.</summary>
internal sealed record LivelineFrame(
    LivelinePoint[] Points,
    DateTimeOffset WindowEnd,
    TimeSpan Window,
    double Glide,
    LivelineState State,
    double AxisMinimum,
    double AxisMaximum,
    double AxisStep,
    double AlertThreshold,
    string AlertLabel,
    string Unit,
    bool Compact,
    int? Inspect,
    double WindowSeconds);

/// <summary>
/// Draws the plot with SkiaSharp: grid and value labels, the alert line, the area at 7% and the 2 px line, the
/// dashed "now" line, the live dot and its value tag, a hatched gap when disconnected, and the inspect crosshair and
/// tooltip. Geometry follows the prototype's 942 x 304 plot: 64 px on the right for the tag, 24 px below for the time
/// labels. Fonts are the bundled Geist and Geist Mono, loaded once from the app package.
/// </summary>
public partial class LivelineChartCanvas : SKCanvasElement
{
    private const float TagArea = 64f;
    private const float TimeArea = 24f;
    private const float Top = 18f;
    private const float Left = 6f;

    private static Task<SKTypeface?>? s_mono;
    private static Task<SKTypeface?>? s_monoMedium;
    private static Task<SKTypeface?>? s_sans;

    private readonly SKPaint _fill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _stroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
    private readonly SKPathEffect _alertDash = SKPathEffect.CreateDash([3f, 5f], 0);
    private readonly SKPathEffect _nowDash = SKPathEffect.CreateDash([2f, 3f], 0);
    private readonly SKFont _axisFont = new(SKTypeface.Default, 11f);
    private readonly SKFont _tagFont = new(SKTypeface.Default, 12f);
    private readonly SKFont _tipValueFont = new(SKTypeface.Default, 12f);
    private readonly SKFont _tipFont = new(SKTypeface.Default, 12f);

    private LivelineFrame? _frame;
    private SKColor _accent, _ink, _muted, _grid, _surface;
    private float _plotLeft, _plotRight;

    public LivelineChartCanvas()
    {
        Loaded += async (_, _) => await LoadFontsAsync();
    }

    internal void Update(LivelineFrame frame)
    {
        _frame = frame;
        Invalidate();
    }

    internal void SetColors(Brush? accent, Brush? ink, Brush? muted, Brush? grid, Brush? surface)
    {
        _accent = ToColor(accent);
        _ink = ToColor(ink);
        _muted = ToColor(muted);
        _grid = ToColor(grid);
        _surface = ToColor(surface);
        Invalidate();
    }

    /// <summary>The sample nearest to x (in this element's coordinates), or null outside the plot.</summary>
    internal int? IndexAt(double x)
    {
        if (_frame is not { Points.Length: > 0 } f || x < _plotLeft - 4 || x > _plotRight + 4)
        {
            return null;
        }

        int? best = null;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < f.Points.Length; i++)
        {
            var px = X(f, f.Points[i].Time);
            if (px < _plotLeft - 1 || px > _plotRight + 1)
            {
                continue;
            }

            var distance = Math.Abs(px - x);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        return best;
    }

    private static SKColor ToColor(Brush? brush)
        => brush is SolidColorBrush s
            ? new SKColor(s.Color.R, s.Color.G, s.Color.B, (byte)(s.Color.A * Math.Clamp(s.Opacity, 0, 1)))
            : SKColors.Transparent;

    private async Task LoadFontsAsync()
    {
        s_mono ??= LoadAsync("GeistMono/GeistMono-Regular.ttf");
        s_monoMedium ??= LoadAsync("GeistMono/GeistMono-Medium.ttf");
        s_sans ??= LoadAsync("Geist/Geist-Regular.ttf");
        var mono = await s_mono;
        var monoMedium = await s_monoMedium;
        var sans = await s_sans;
        if (mono is not null)
        {
            _axisFont.Typeface = mono;
            _tagFont.Typeface = mono;
        }

        if (monoMedium is not null)
        {
            _tipValueFont.Typeface = monoMedium;
        }

        if (sans is not null)
        {
            _tipFont.Typeface = sans;
        }

        Invalidate();
    }

    private static async Task<SKTypeface?> LoadAsync(string file)
    {
        try
        {
            var storage = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Fonts/" + file));
            using var stream = await storage.OpenStreamForReadAsync();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            memory.Position = 0;
            return SKTypeface.FromStream(memory);
        }
        catch (Exception)
        {
            // The labels fall back to the platform's default typeface.
            return null;
        }
    }

    private float _plotTop, _plotBottom;

    private float X(LivelineFrame f, DateTimeOffset t)
    {
        var fraction = 1 - (f.WindowEnd - t).TotalMilliseconds / f.Window.TotalMilliseconds;
        return _plotLeft + (float)fraction * (_plotRight - _plotLeft);
    }

    private float Y(LivelineFrame f, double v)
    {
        var fraction = (v - f.AxisMinimum) / (f.AxisMaximum - f.AxisMinimum);
        return _plotBottom - (float)fraction * (_plotBottom - _plotTop);
    }

    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        if (_frame is not { } f || area.Width < 40 || area.Height < 40)
        {
            return;
        }

        var w = (float)area.Width;
        var h = (float)area.Height;
        _plotLeft = Left;
        _plotRight = w - (f.Compact ? TagArea - 4 : TagArea);
        _plotTop = Top;
        _plotBottom = h - TimeArea;

        var connecting = f.State == LivelineState.Connecting || f.Points.Length == 0;

        DrawGrid(canvas, f, connecting);
        if (connecting)
        {
            DrawWaiting(canvas, f);
            return;
        }

        DrawTimeLabels(canvas, f);
        DrawAlert(canvas, f);

        var stopped = f.State is LivelineState.Paused or LivelineState.Disconnected;
        var lineColor = f.State == LivelineState.Paused ? _muted : _accent;

        canvas.Save();
        canvas.ClipRect(new SKRect(_plotLeft, 0, _plotRight, h));
        DrawSeries(canvas, f, lineColor);
        if (f.State == LivelineState.Disconnected)
        {
            DrawGap(canvas, f);
        }

        canvas.Restore();

        // The dashed "now" line at the plot's right edge.
        _stroke.Color = _ink.WithAlpha(56);
        _stroke.StrokeWidth = 1;
        _stroke.PathEffect = _nowDash;
        canvas.DrawLine(_plotRight, _plotTop, _plotRight, _plotBottom, _stroke);
        _stroke.PathEffect = null;

        DrawLiveMark(canvas, f, stopped);
        DrawInspect(canvas, f);
    }

    private void DrawGrid(SKCanvas canvas, LivelineFrame f, bool connecting)
    {
        _stroke.Color = _grid;
        _stroke.StrokeWidth = 1;
        _fill.Color = _muted;
        for (var v = f.AxisMinimum; v <= f.AxisMaximum + 1e-9; v += f.AxisStep)
        {
            var y = MathF.Round(Y(f, v)) + 0.5f;
            canvas.DrawLine(_plotLeft, y, _plotRight, y, _stroke);
            if (!connecting)
            {
                var label = v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                if (v + f.AxisStep > f.AxisMaximum + 1e-9 && !f.Compact && !string.IsNullOrEmpty(f.Unit))
                {
                    label += " " + f.Unit;
                }

                canvas.DrawText(label, _plotLeft, y - 6, SKTextAlign.Left, _axisFont, _fill);
            }
        }
    }

    private void DrawWaiting(SKCanvas canvas, LivelineFrame f)
    {
        // The tag's space is kept: an outlined pill with a dash, and one line of text in the plot.
        var midY = (_plotTop + _plotBottom) / 2;
        var tag = new SKRect(_plotRight + 8, midY - 11, _plotRight + 8 + 44, midY + 11);
        _stroke.Color = _grid;
        _stroke.StrokeWidth = 1.5f;
        canvas.DrawRoundRect(tag, 11, 11, _stroke);
        _fill.Color = _muted;
        canvas.DrawText("—", tag.MidX, midY + 4, SKTextAlign.Center, _axisFont, _fill);
        canvas.DrawText("Waiting for the first value", (_plotLeft + _plotRight) / 2, midY + 4, SKTextAlign.Center, _tipFont, _fill);
    }

    private void DrawTimeLabels(SKCanvas canvas, LivelineFrame f)
    {
        _fill.Color = _muted;
        var y = _plotBottom + TimeArea - 4;
        var steps = f.Compact ? 2 : 4;
        for (var i = 0; i <= steps; i++)
        {
            var x = _plotLeft + (_plotRight - _plotLeft) * i / steps;
            var align = i == 0 ? SKTextAlign.Left : i == steps ? SKTextAlign.Right : SKTextAlign.Center;
            canvas.DrawText(LivelineChart.Ago(f.WindowSeconds * (1 - (double)i / steps)), x, y, align, _axisFont, _fill);
        }
    }

    private void DrawAlert(SKCanvas canvas, LivelineFrame f)
    {
        if (double.IsNaN(f.AlertThreshold) || f.AlertThreshold < f.AxisMinimum || f.AlertThreshold > f.AxisMaximum)
        {
            return;
        }

        var y = MathF.Round(Y(f, f.AlertThreshold)) + 0.5f;
        _stroke.Color = _ink.WithAlpha(140);
        _stroke.StrokeWidth = 1;
        _stroke.PathEffect = _alertDash;
        canvas.DrawLine(_plotLeft, y, _plotRight, y, _stroke);
        _stroke.PathEffect = null;
        if (!f.Compact && !string.IsNullOrEmpty(f.AlertLabel))
        {
            _fill.Color = _ink;
            canvas.DrawText(f.AlertLabel, _plotLeft + 74, y - 6, SKTextAlign.Left, _axisFont, _fill);
        }
    }

    private void DrawSeries(SKCanvas canvas, LivelineFrame f, SKColor lineColor)
    {
        if (f.Points.Length < 2)
        {
            return;
        }

        using var line = new SKPathBuilder();
        using var areaPath = new SKPathBuilder();
        var first = f.Points[0];
        line.MoveTo(X(f, first.Time), Y(f, first.Value));
        areaPath.MoveTo(X(f, first.Time), _plotBottom);
        areaPath.LineTo(X(f, first.Time), Y(f, first.Value));
        for (var i = 1; i < f.Points.Length; i++)
        {
            var p = f.Points[i];
            line.LineTo(X(f, p.Time), Y(f, p.Value));
            areaPath.LineTo(X(f, p.Time), Y(f, p.Value));
        }

        areaPath.LineTo(X(f, f.Points[^1].Time), _plotBottom);
        areaPath.Close();

        using var area = areaPath.Detach();
        _fill.Color = _accent.WithAlpha(18);
        canvas.DrawPath(area, _fill);

        using var path = line.Detach();
        _stroke.Color = lineColor;
        _stroke.StrokeWidth = 2;
        canvas.DrawPath(path, _stroke);
    }

    private void DrawGap(SKCanvas canvas, LivelineFrame f)
    {
        // Disconnected: hatch from the last sample to now, never extrapolate.
        var x0 = X(f, f.Points[^1].Time);
        if (_plotRight - x0 < 2)
        {
            return;
        }

        canvas.Save();
        canvas.ClipRect(new SKRect(x0 + 6, _plotTop, _plotRight, _plotBottom));
        _stroke.Color = _grid;
        _stroke.StrokeWidth = 2;
        var height = _plotBottom - _plotTop;
        for (var x = x0 - height; x < _plotRight + height; x += 8)
        {
            canvas.DrawLine(x, _plotBottom, x + height, _plotTop, _stroke);
        }

        canvas.Restore();
    }

    private void DrawLiveMark(SKCanvas canvas, LivelineFrame f, bool stopped)
    {
        var last = f.Points[^1];
        float x, y;
        if (f.State == LivelineState.Disconnected)
        {
            // The dot stays on the last sample, hollow.
            x = X(f, last.Time);
            y = Y(f, last.Value);
            _fill.Color = _surface;
            canvas.DrawCircle(x, y, 4.5f, _fill);
            _stroke.Color = _muted;
            _stroke.StrokeWidth = 2;
            canvas.DrawCircle(x, y, 4.5f, _stroke);
        }
        else
        {
            // The dot sits on the "now" line and moves to the newest value as the line glides in.
            var previous = f.Points.Length > 1 ? f.Points[^2].Value : last.Value;
            var value = previous + (last.Value - previous) * f.Glide;
            x = _plotRight;
            y = Y(f, value);
            _fill.Color = stopped ? _muted : _accent;
            canvas.DrawCircle(x, y, 5, _fill);
            _stroke.Color = _surface;
            _stroke.StrokeWidth = 2;
            canvas.DrawCircle(x, y, 5, _stroke);
        }

        var text = LivelineChart.Format(last.Value);
        var tagWidth = f.Compact ? 52f : 56f;
        var tag = new SKRect(_plotRight + 8, y - 12, _plotRight + 8 + tagWidth, y + 12);
        _fill.Color = stopped ? _muted : _ink;
        canvas.DrawRoundRect(tag, 12, 12, _fill);
        _fill.Color = _surface;
        canvas.DrawText(text, tag.MidX, y + 4, SKTextAlign.Center, _tagFont, _fill);
    }

    private void DrawInspect(SKCanvas canvas, LivelineFrame f)
    {
        if (f.Inspect is not { } i || i >= f.Points.Length)
        {
            return;
        }

        var p = f.Points[i];
        var x = X(f, p.Time);
        var y = Y(f, p.Value);
        _stroke.Color = _ink;
        _stroke.StrokeWidth = 1;
        canvas.DrawLine(x, _plotTop, x, _plotBottom, _stroke);
        _fill.Color = _accent;
        canvas.DrawCircle(x, y, 5, _fill);
        _stroke.Color = _surface;
        _stroke.StrokeWidth = 2;
        canvas.DrawCircle(x, y, 5, _stroke);

        // Tooltip: value (mono, medium) over how long ago, ink plate, 12 px above the point.
        var value = LivelineChart.Format(p.Value) + " " + f.Unit;
        var ago = LivelineChart.Ago((f.Points[^1].Time - p.Time).TotalSeconds);
        var width = Math.Max(_tipValueFont.MeasureText(value), _tipFont.MeasureText(ago)) + 20;
        const float height = 42;
        var left = Math.Clamp(x - width / 2, _plotLeft, _plotRight + TagArea - width);
        var top = y - 12 - height;
        if (top < 0)
        {
            top = y + 12;
        }

        _fill.Color = _ink;
        canvas.DrawRoundRect(new SKRect(left, top, left + width, top + height), 8, 8, _fill);
        _fill.Color = _surface;
        canvas.DrawText(value, left + 10, top + 18, SKTextAlign.Left, _tipValueFont, _fill);
        _fill.Color = _surface.WithAlpha(191);
        canvas.DrawText(ago, left + 10, top + 34, SKTextAlign.Left, _tipFont, _fill);
    }
}
