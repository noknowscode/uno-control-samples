using System.Globalization;
using System.Text.RegularExpressions;

namespace AdaptiveInputDemo.Services;

/// <summary>What the text reads as.</summary>
public enum DetectedInputType
{
    None,
    Date,
    Color,
    Mention,
    Tag,
}

/// <summary>
/// Result of detection. <see cref="Value"/> is the colour hex, the mention query or the tag name;
/// <see cref="Date"/> and <see cref="Time"/> are set for dates.
/// </summary>
public sealed record InputDetectionResult(DetectedInputType Type, string Value, DateTime? Date = null, TimeSpan? Time = null);

/// <summary>
/// Reads dates (today, tomorrow, tmrw, next week, a weekday, "nov 30", with an optional "3pm" or "3:30 pm"),
/// #colours (#RGB or #RRGGBB), @people and #tags, in that order of rules from the design.
/// </summary>
public static partial class InputTypeDetector
{
    private static readonly string[] Weekdays = ["sun", "mon", "tue", "wed", "thu", "fri", "sat"];
    private static readonly string[] Months = ["jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec"];

    [GeneratedRegex(@"^#([0-9a-f]{3}|[0-9a-f]{6})$", RegexOptions.IgnoreCase)]
    private static partial Regex ColorPattern();

    [GeneratedRegex(@"^@\w*$")]
    private static partial Regex MentionPattern();

    [GeneratedRegex(@"^#[a-z][\w-]*$", RegexOptions.IgnoreCase)]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"\s+(\d{1,2})(?::(\d{2}))?\s*(am|pm)$")]
    private static partial Regex TimePattern();

    [GeneratedRegex(@"^([a-z]{3})[a-z]*\s+(\d{1,2})$")]
    private static partial Regex MonthDayPattern();

    [GeneratedRegex(@"^(today|tomorrow|tmrw|tonight|next|this|last|in|on|sun|mon|tue|wed|thu|fri|sat|jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)\b|\d\s*(am|pm)$")]
    private static partial Regex DateLikePattern();

    public static InputDetectionResult? Detect(string? text, DateTime today)
    {
        var s = text?.Trim() ?? string.Empty;
        if (s.Length == 0) return null;
        if (ColorPattern().IsMatch(s)) return new(DetectedInputType.Color, s);
        if (MentionPattern().IsMatch(s)) return new(DetectedInputType.Mention, s[1..]);
        if (TagPattern().IsMatch(s)) return new(DetectedInputType.Tag, s[1..]);
        if (ParseDate(s, today) is { } d) return new(DetectedInputType.Date, s, d.Date, d.Time);
        return null;
    }

    /// <summary>True when unrecognised text starts like a date ("next blursday", "fri 25pm"): the field offers the picker.</summary>
    public static bool LooksLikeDate(string? text) => DateLikePattern().IsMatch(text?.Trim().ToLowerInvariant() ?? string.Empty);

    public static (DateTime Date, TimeSpan? Time)? ParseDate(string text, DateTime today)
    {
        var s = text.Trim().ToLowerInvariant();
        TimeSpan? time = null;
        var tm = TimePattern().Match(s);
        if (tm.Success)
        {
            var h = int.Parse(tm.Groups[1].Value, CultureInfo.InvariantCulture);
            var m = tm.Groups[2].Success ? int.Parse(tm.Groups[2].Value, CultureInfo.InvariantCulture) : 0;
            if (h is < 1 or > 12 || m > 59) return null;
            time = new TimeSpan(h % 12 + (tm.Groups[3].Value == "pm" ? 12 : 0), m, 0);
            s = s[..tm.Index];
        }

        DateTime? d = s switch
        {
            "today" => today,
            "tomorrow" or "tmrw" => today.AddDays(1),
            "next week" => today.AddDays((8 - (int)today.DayOfWeek) % 7 is 0 ? 7 : (8 - (int)today.DayOfWeek) % 7),
            _ => null,
        };

        if (d == null)
        {
            var wi = Array.FindIndex(Weekdays, w => s == w || (s.StartsWith(w, StringComparison.Ordinal) && s.Length <= 9 && s.EndsWith("day", StringComparison.Ordinal)));
            if (wi >= 0)
            {
                var ahead = (wi - (int)today.DayOfWeek + 7) % 7;
                d = today.AddDays(ahead == 0 ? 7 : ahead);
            }
        }

        if (d == null)
        {
            var md = MonthDayPattern().Match(s);
            var mi = md.Success ? Array.IndexOf(Months, md.Groups[1].Value) : -1;
            if (mi >= 0)
            {
                var day = int.Parse(md.Groups[2].Value, CultureInfo.InvariantCulture);
                var year = mi + 1 < today.Month ? today.Year + 1 : today.Year;
                if (day >= 1 && day <= DateTime.DaysInMonth(year, mi + 1)) d = new DateTime(year, mi + 1, day);
            }
        }

        return d is { } date ? (date.Date, time) : null;
    }

    /// <summary>"Thu, Nov 26" or "Fri, Nov 27 · 3:00 PM".</summary>
    public static string FormatDate(DateTime date, TimeSpan? time = null)
    {
        var text = date.ToString("ddd, MMM d", CultureInfo.InvariantCulture);
        return time is { } t ? text + " · " + DateTime.Today.Add(t).ToString("h:mm tt", CultureInfo.InvariantCulture) : text;
    }

    public static string ExpandHex(string hex)
    {
        var h = hex.TrimStart('#');
        if (h.Length == 3) h = string.Concat(h.Select(c => $"{c}{c}"));
        return "#" + h.ToUpperInvariant();
    }

    public static Windows.UI.Color ToColor(string hex)
    {
        var h = ExpandHex(hex)[1..];
        return Windows.UI.Color.FromArgb(255,
            byte.Parse(h[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(h[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(h[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }
}
