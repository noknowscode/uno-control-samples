using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace PrecisionDial.Controls;

/// <summary>
/// Keyboard, focus and automation for <see cref="PrecisionDial"/>.
/// Value mode: ↑ → step up, ↓ ← step down (<see cref="PrecisionDial.FineStep"/> with Shift or fine mode),
/// PgUp / PgDn a tenth of the range, Home / End the ends. Menu mode: ↑ → next item, ↓ ← previous item,
/// Home / End first and last, Enter / Space confirm.
/// </summary>
public sealed partial class PrecisionDial
{
    private void InitializeInput()
    {
        IsTabStop = true;
        UseSystemFocusVisuals = true;
        KeyDown += OnDialKeyDown;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new PrecisionDialAutomationPeer(this);

    private double KeyStep => IsFineActive ? FineStep : ValueStep > 0 ? ValueStep : 1;

    private void OnDialKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!IsInputEnabled || e.Handled) return;

        if (DialMode == DialMode.Menu)
        {
            var count = MenuItems?.Count ?? 0;
            if (count == 0) return;
            switch (e.Key)
            {
                case VirtualKey.Up: case VirtualKey.Right: MoveTo(Math.Min(Value + 1, count - 1)); break;
                case VirtualKey.Down: case VirtualKey.Left: MoveTo(Math.Max(Value - 1, 0)); break;
                case VirtualKey.Home: MoveTo(0); break;
                case VirtualKey.End: MoveTo(count - 1); break;
                case VirtualKey.Enter: case VirtualKey.Space: RaiseSelectionConfirmedIfMenuMode(); break;
                default: return;
            }
        }
        else
        {
            var page = (Maximum - Minimum) / 10;
            switch (e.Key)
            {
                case VirtualKey.Up: case VirtualKey.Right: MoveTo(Value + KeyStep); break;
                case VirtualKey.Down: case VirtualKey.Left: MoveTo(Value - KeyStep); break;
                case VirtualKey.PageUp: MoveTo(Value + page); break;
                case VirtualKey.PageDown: MoveTo(Value - page); break;
                case VirtualKey.Home: MoveTo(Minimum); break;
                case VirtualKey.End: MoveTo(Maximum); break;
                default: return;
            }
        }
        e.Handled = true;
    }

    /// <summary>Moves to a value the way a drag does: clamped, with the detent click when it crosses one.</summary>
    internal void MoveTo(double value)
    {
        Value = Math.Clamp(Math.Round(value, 4), Minimum, Maximum);
        var detent = CurrentDetentIndex;
        if (detent != _lastDetentIndex)
        {
            OnDetentCrossed(detent);
            _lastDetentIndex = detent;
        }
    }

    private void RaiseAutomationChanged(double oldValue, double newValue)
    {
        if (FrameworkElementAutomationPeer.FromElement(this) is not PrecisionDialAutomationPeer peer) return;

        if (DialMode == DialMode.Menu)
            peer.RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, string.Empty, SelectedItem?.Label ?? string.Empty);
        else
            peer.RaisePropertyChangedEvent(RangeValuePatternIdentifiers.ValueProperty, oldValue, newValue);
    }
}

/// <summary>
/// Value mode: a slider (RangeValue pattern). Menu mode: a spinner whose Value pattern is the selected item's label.
/// </summary>
public sealed partial class PrecisionDialAutomationPeer : FrameworkElementAutomationPeer, IRangeValueProvider, IValueProvider
{
    public PrecisionDialAutomationPeer(PrecisionDial owner) : base(owner)
    {
    }

    private PrecisionDial Dial => (PrecisionDial)Owner;

    private bool IsMenu => Dial.DialMode == DialMode.Menu;

    protected override string GetClassNameCore() => nameof(PrecisionDial);

    protected override AutomationControlType GetAutomationControlTypeCore()
        => IsMenu ? AutomationControlType.Spinner : AutomationControlType.Slider;

    protected override object? GetPatternCore(PatternInterface patternInterface) => patternInterface switch
    {
        PatternInterface.RangeValue when !IsMenu => this,
        PatternInterface.Value when IsMenu => this,
        _ => base.GetPatternCore(patternInterface),
    };

    // IRangeValueProvider (Value mode)
    double IRangeValueProvider.Value => Dial.Value;

    public double Minimum => Dial.Minimum;

    public double Maximum => Dial.Maximum;

    public double SmallChange => Dial.IsFineMode ? Dial.FineStep : Dial.ValueStep > 0 ? Dial.ValueStep : 1;

    public double LargeChange => (Dial.Maximum - Dial.Minimum) / 10;

    public bool IsReadOnly => !Dial.IsInputEnabled;

    public void SetValue(double value) => Dial.MoveTo(value);

    // IValueProvider (Menu mode): the selected item's label; setting a label selects that item.
    string IValueProvider.Value => Dial.SelectedItem?.Label ?? string.Empty;

    public void SetValue(string value)
    {
        var items = Dial.MenuItems;
        if (items is null) return;
        for (int i = 0; i < items.Count; i++)
        {
            if (string.Equals(items[i].Label, value, StringComparison.OrdinalIgnoreCase))
            {
                Dial.MoveTo(i);
                return;
            }
        }
    }
}
