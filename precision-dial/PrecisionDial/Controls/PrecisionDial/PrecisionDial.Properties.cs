using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace PrecisionDial.Controls;

public sealed partial class PrecisionDial
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(PrecisionDial),
            new PropertyMetadata(0.0, OnValuePropertyChanged));
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    private static void OnValuePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    { if (d is PrecisionDial dial) dial.OnValueChanged((double)e.OldValue, (double)e.NewValue); }

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(PrecisionDial), new PropertyMetadata(0.0));
    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(PrecisionDial), new PropertyMetadata(100.0));
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    public static readonly DependencyProperty DetentCountProperty =
        DependencyProperty.Register(nameof(DetentCount), typeof(int), typeof(PrecisionDial), new PropertyMetadata(20));
    public int DetentCount { get => (int)GetValue(DetentCountProperty); set => SetValue(DetentCountProperty, value); }

    public static readonly DependencyProperty IsHapticEnabledProperty =
        DependencyProperty.Register(nameof(IsHapticEnabled), typeof(bool), typeof(PrecisionDial), new PropertyMetadata(true));
    public bool IsHapticEnabled { get => (bool)GetValue(IsHapticEnabledProperty); set => SetValue(IsHapticEnabledProperty, value); }

    public static readonly DependencyProperty ArcSweepDegreesProperty =
        DependencyProperty.Register(nameof(ArcSweepDegrees), typeof(double), typeof(PrecisionDial), new PropertyMetadata(270.0));
    public double ArcSweepDegrees { get => (double)GetValue(ArcSweepDegreesProperty); set => SetValue(ArcSweepDegreesProperty, value); }

    public static readonly DependencyProperty AccentBrushProperty =
        DependencyProperty.Register(nameof(AccentBrush), typeof(Brush), typeof(PrecisionDial),
            new PropertyMetadata(null, (d, _) => (d as PrecisionDial)?.InvalidateCanvas()));
    public Brush AccentBrush { get => (Brush)GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }

    public static readonly DependencyProperty SensitivityProperty =
        DependencyProperty.Register(nameof(Sensitivity), typeof(double), typeof(PrecisionDial), new PropertyMetadata(0.4));
    public double Sensitivity { get => (double)GetValue(SensitivityProperty); set => SetValue(SensitivityProperty, value); }

    public event EventHandler<DialValueChangedEventArgs>? ValueChanged;
    public event EventHandler<DetentCrossedEventArgs>? DetentCrossed;
    public event RoutedEventHandler? DragStarted;
    public event RoutedEventHandler? DragCompleted;

    // ── v2: New Dependency Properties ──

    public static readonly DependencyProperty IsAudioEnabledProperty =
        DependencyProperty.Register(nameof(IsAudioEnabled), typeof(bool), typeof(PrecisionDial), new PropertyMetadata(true));
    public bool IsAudioEnabled { get => (bool)GetValue(IsAudioEnabledProperty); set => SetValue(IsAudioEnabledProperty, value); }

    public static readonly DependencyProperty IsInertiaEnabledProperty =
        DependencyProperty.Register(nameof(IsInertiaEnabled), typeof(bool), typeof(PrecisionDial), new PropertyMetadata(true));
    public bool IsInertiaEnabled { get => (bool)GetValue(IsInertiaEnabledProperty); set => SetValue(IsInertiaEnabledProperty, value); }

    public static readonly DependencyProperty InteractionModeProperty =
        DependencyProperty.Register(nameof(InteractionMode), typeof(DialInteractionMode), typeof(PrecisionDial), new PropertyMetadata(DialInteractionMode.Auto));
    public DialInteractionMode InteractionMode { get => (DialInteractionMode)GetValue(InteractionModeProperty); set => SetValue(InteractionModeProperty, value); }

    public static readonly DependencyProperty InertiaDecayRateProperty =
        DependencyProperty.Register(nameof(InertiaDecayRate), typeof(double), typeof(PrecisionDial), new PropertyMetadata(0.92));
    public double InertiaDecayRate { get => (double)GetValue(InertiaDecayRateProperty); set => SetValue(InertiaDecayRateProperty, value); }

    public event RoutedEventHandler? InertiaCompleted;

    // ── v3: Dial mode + radial menu ──────────────────────────────────────────

    public static readonly DependencyProperty DialModeProperty =
        DependencyProperty.Register(nameof(DialMode), typeof(DialMode), typeof(PrecisionDial),
            new PropertyMetadata(DialMode.Value, OnDialModePropertyChanged));
    public DialMode DialMode
    {
        get => (DialMode)GetValue(DialModeProperty);
        set => SetValue(DialModeProperty, value);
    }
    private static void OnDialModePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PrecisionDial dial) dial.OnDialModeChanged();
    }

    public static readonly DependencyProperty MenuItemsProperty =
        DependencyProperty.Register(nameof(MenuItems), typeof(IList<DialMenuItem>), typeof(PrecisionDial),
            new PropertyMetadata(null, OnMenuItemsPropertyChanged));
    public IList<DialMenuItem>? MenuItems
    {
        get => (IList<DialMenuItem>?)GetValue(MenuItemsProperty);
        set => SetValue(MenuItemsProperty, value);
    }
    private static void OnMenuItemsPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PrecisionDial dial) dial.OnMenuItemsChanged();
    }

    public static readonly DependencyProperty SelectedIndexProperty =
        DependencyProperty.Register(nameof(SelectedIndex), typeof(int), typeof(PrecisionDial),
            new PropertyMetadata(0, OnSelectedIndexPropertyChanged));
    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }
    private static void OnSelectedIndexPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PrecisionDial dial) dial.OnSelectedIndexChanged((int)e.OldValue, (int)e.NewValue);
    }

    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(nameof(SelectedItem), typeof(DialMenuItem), typeof(PrecisionDial),
            new PropertyMetadata(null));
    public DialMenuItem? SelectedItem
    {
        get => (DialMenuItem?)GetValue(SelectedItemProperty);
        private set => SetValue(SelectedItemProperty, value);
    }

    /// <summary>
    /// Override segment count. -1 = auto-scale from control size via DialSizeProfile.
    /// In Menu mode this is ignored — segment count always equals MenuItems.Count.
    /// </summary>
    public static readonly DependencyProperty SegmentCountProperty =
        DependencyProperty.Register(nameof(SegmentCount), typeof(int), typeof(PrecisionDial),
            new PropertyMetadata(-1, (d, _) => (d as PrecisionDial)?.InvalidateCanvas()));
    public int SegmentCount
    {
        get => (int)GetValue(SegmentCountProperty);
        set => SetValue(SegmentCountProperty, value);
    }

    public event EventHandler<MenuSelectionEventArgs>? SelectionConfirmed;

    // ── Control Gallery sheet: continuous ring, hot range, fine steps ───────

    public static readonly DependencyProperty ArcStyleProperty =
        DependencyProperty.Register(nameof(ArcStyle), typeof(DialArcStyle), typeof(PrecisionDial),
            new PropertyMetadata(DialArcStyle.Dashed, (d, _) => (d as PrecisionDial)?.InvalidateCanvas()));
    /// <summary>Dashed (segmented arc with an orbiting value) or Continuous (the sheet's ring with 44 ticks).</summary>
    public DialArcStyle ArcStyle { get => (DialArcStyle)GetValue(ArcStyleProperty); set => SetValue(ArcStyleProperty, value); }

    public static readonly DependencyProperty HotThresholdProperty =
        DependencyProperty.Register(nameof(HotThreshold), typeof(double), typeof(PrecisionDial),
            new PropertyMetadata(double.NaN, (d, _) => (d as PrecisionDial)?.InvalidateCanvas()));
    /// <summary>Value past which the continuous arc and its ticks turn <see cref="HotBrush"/>; NaN turns it off.</summary>
    public double HotThreshold { get => (double)GetValue(HotThresholdProperty); set => SetValue(HotThresholdProperty, value); }

    public static readonly DependencyProperty HotBrushProperty =
        DependencyProperty.Register(nameof(HotBrush), typeof(Brush), typeof(PrecisionDial),
            new PropertyMetadata(null, (d, _) => (d as PrecisionDial)?.InvalidateCanvas()));
    public Brush HotBrush { get => (Brush)GetValue(HotBrushProperty); set => SetValue(HotBrushProperty, value); }

    public static readonly DependencyProperty ValueStepProperty =
        DependencyProperty.Register(nameof(ValueStep), typeof(double), typeof(PrecisionDial), new PropertyMetadata(0.0));
    /// <summary>Dragging rounds the value to this step (0 keeps it continuous).</summary>
    public double ValueStep { get => (double)GetValue(ValueStepProperty); set => SetValue(ValueStepProperty, value); }

    public static readonly DependencyProperty FineStepProperty =
        DependencyProperty.Register(nameof(FineStep), typeof(double), typeof(PrecisionDial), new PropertyMetadata(0.1));
    /// <summary>Step in fine mode (Shift held or <see cref="IsFineMode"/>); dragging is ten times slower.</summary>
    public double FineStep { get => (double)GetValue(FineStepProperty); set => SetValue(FineStepProperty, value); }

    public static readonly DependencyProperty IsFineModeProperty =
        DependencyProperty.Register(nameof(IsFineMode), typeof(bool), typeof(PrecisionDial), new PropertyMetadata(false));
    public bool IsFineMode { get => (bool)GetValue(IsFineModeProperty); set => SetValue(IsFineModeProperty, value); }

    public static readonly DependencyProperty ShowValueTipProperty =
        DependencyProperty.Register(nameof(ShowValueTip), typeof(bool), typeof(PrecisionDial),
            new PropertyMetadata(true, (d, _) => (d as PrecisionDial)?.InvalidateCanvas()));
    /// <summary>Shows the value next to the arc (the orbiting value, or the continuous ring's tip).</summary>
    public bool ShowValueTip { get => (bool)GetValue(ShowValueTipProperty); set => SetValue(ShowValueTipProperty, value); }

    public static readonly DependencyProperty IsInputEnabledProperty =
        DependencyProperty.Register(nameof(IsInputEnabled), typeof(bool), typeof(PrecisionDial),
            new PropertyMetadata(true, (d, _) => (d as PrecisionDial)?.InvalidateCanvas()));
    /// <summary>False ignores input and draws the dial at 45 % (a Panel has no IsEnabled; a locked preset uses this).</summary>
    public bool IsInputEnabled { get => (bool)GetValue(IsInputEnabledProperty); set => SetValue(IsInputEnabledProperty, value); }
}
