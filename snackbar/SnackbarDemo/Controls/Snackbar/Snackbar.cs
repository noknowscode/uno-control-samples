using System;
using System.Diagnostics;

#if IS_WINUI
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
#else
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
#endif

namespace Uno.Toolkit.UI
{
	/// <summary>
	/// A short message docked at the bottom of a <see cref="SnackbarHost"/>: an optional icon, the message, an optional
	/// action and close button, and a 2 px track that drains while the countdown runs. Hover, keyboard focus or a
	/// finger on it pauses the countdown.
	/// </summary>
	[TemplatePart(Name = TemplateParts.ActionButtonName, Type = typeof(Button))]
	[TemplatePart(Name = TemplateParts.DismissButtonName, Type = typeof(Button))]
	[TemplatePart(Name = TemplateParts.DrainScaleName, Type = typeof(ScaleTransform))]
	[TemplateVisualState(GroupName = VisualStateNames.LayoutStates, Name = VisualStateNames.SingleLine)]
	[TemplateVisualState(GroupName = VisualStateNames.LayoutStates, Name = VisualStateNames.MultiLine)]
	[TemplateVisualState(GroupName = VisualStateNames.ActionStates, Name = VisualStateNames.NoAction)]
	[TemplateVisualState(GroupName = VisualStateNames.ActionStates, Name = VisualStateNames.WithAction)]
	[TemplateVisualState(GroupName = VisualStateNames.ActionStates, Name = VisualStateNames.WithActionOnNewLine)]
	[TemplateVisualState(GroupName = VisualStateNames.DismissStates, Name = VisualStateNames.NoDismiss)]
	[TemplateVisualState(GroupName = VisualStateNames.DismissStates, Name = VisualStateNames.WithDismiss)]
	[TemplateVisualState(GroupName = VisualStateNames.IconStates, Name = VisualStateNames.NoIcon)]
	[TemplateVisualState(GroupName = VisualStateNames.IconStates, Name = VisualStateNames.CustomIcon)]
	[TemplateVisualState(GroupName = VisualStateNames.IconStates, Name = VisualStateNames.AttentionIcon)]
	[TemplateVisualState(GroupName = VisualStateNames.TimerStates, Name = VisualStateNames.Untimed)]
	[TemplateVisualState(GroupName = VisualStateNames.TimerStates, Name = VisualStateNames.Timed)]
	[TemplateVisualState(GroupName = VisualStateNames.HoldStates, Name = VisualStateNames.Running)]
	[TemplateVisualState(GroupName = VisualStateNames.HoldStates, Name = VisualStateNames.Held)]
	public partial class Snackbar : Control
	{
		internal static class TemplateParts
		{
			public const string ActionButtonName = "PART_ActionButton";
			public const string DismissButtonName = "PART_DismissButton";
			public const string DrainScaleName = "PART_DrainScale";
		}

		private static class VisualStateNames
		{
			public const string LayoutStates = nameof(LayoutStates);
			public const string SingleLine = nameof(SingleLine);
			public const string MultiLine = nameof(MultiLine);

			public const string ActionStates = nameof(ActionStates);
			public const string NoAction = nameof(NoAction);
			public const string WithAction = nameof(WithAction);
			public const string WithActionOnNewLine = nameof(WithActionOnNewLine);

			public const string DismissStates = nameof(DismissStates);
			public const string NoDismiss = nameof(NoDismiss);
			public const string WithDismiss = nameof(WithDismiss);

			public const string IconStates = nameof(IconStates);
			public const string NoIcon = nameof(NoIcon);
			public const string CustomIcon = nameof(CustomIcon);
			public const string AttentionIcon = nameof(AttentionIcon);

			public const string TimerStates = nameof(TimerStates);
			public const string Untimed = nameof(Untimed);
			public const string Timed = nameof(Timed);

			public const string HoldStates = nameof(HoldStates);
			public const string Running = nameof(Running);
			public const string Held = nameof(Held);
		}

		private Button? _actionButton;
		private Button? _dismissButton;
		private ScaleTransform? _drainScale;
		private bool _isReady;

		// Countdown: a one-shot timer does the timing, the storyboard only paints the draining track.
		private readonly DispatcherTimer _timer = new();
		private readonly Stopwatch _segment = new();
		private Storyboard? _drain;
		private TimeSpan _remaining;
		private bool _counting;

		// Hold sources: any of them pauses the countdown.
		private bool _pointerOver;
		private bool _focusWithin;
		private bool _touchDown;

		/// <summary>Occurs when the action button is clicked.</summary>
		public event EventHandler? ActionClicked;

		/// <summary>Occurs when the close button is clicked or Escape is pressed.</summary>
		public event EventHandler? DismissClicked;

		/// <summary>Occurs when the countdown started by the host runs out.</summary>
		public event EventHandler? Expired;

		public Snackbar()
		{
			DefaultStyleKey = typeof(Snackbar);
			AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);

			_timer.Tick += OnTimerTick;
			KeyDown += OnKeyDown;
			PointerEntered += (_, _) => { _pointerOver = true; UpdateHold(); };
			PointerExited += (_, _) => { _pointerOver = false; UpdateHold(); };
			PointerPressed += OnPointerPressed;
			PointerReleased += OnPointerUp;
			PointerCanceled += OnPointerUp;
			PointerCaptureLost += OnPointerUp;
			GotFocus += (_, _) => { _focusWithin = true; UpdateHold(); };
			LostFocus += OnLostFocus;
			Unloaded += (_, _) => StopCountdown();
		}

		protected override void OnApplyTemplate()
		{
			if (_actionButton != null) _actionButton.Click -= OnActionButtonClick;
			if (_dismissButton != null) _dismissButton.Click -= OnDismissButtonClick;

			base.OnApplyTemplate();

			_actionButton = GetTemplateChild(TemplateParts.ActionButtonName) as Button;
			_dismissButton = GetTemplateChild(TemplateParts.DismissButtonName) as Button;
			_drainScale = GetTemplateChild(TemplateParts.DrainScaleName) as ScaleTransform;

			if (_actionButton != null) _actionButton.Click += OnActionButtonClick;
			if (_dismissButton != null) _dismissButton.Click += OnDismissButtonClick;

			_isReady = true;
			ApplyProgress();
			UpdateVisualStates(useTransitions: false);
		}

		/// <summary>Whether the snackbar uses the two-row layout (Attention, a long message, or <see cref="IsActionOnNewLine"/>).</summary>
		public bool IsMultiLine
			=> Kind == SnackbarKind.Attention || IsActionOnNewLine || (Message?.Length ?? 0) > MultiLineThreshold;

		private void OnLayoutPropertiesChanged() => UpdateVisualStates(useTransitions: true);

		private void UpdateVisualStates(bool useTransitions)
		{
			if (!_isReady) return;

			var multiLine = IsMultiLine;
			Go(multiLine ? VisualStateNames.MultiLine : VisualStateNames.SingleLine);

			Go(string.IsNullOrEmpty(ActionLabel) ? VisualStateNames.NoAction
				: multiLine ? VisualStateNames.WithActionOnNewLine
				: VisualStateNames.WithAction);

			Go(ShowDismissButton || Kind == SnackbarKind.Attention ? VisualStateNames.WithDismiss : VisualStateNames.NoDismiss);

			Go(Icon != null ? VisualStateNames.CustomIcon
				: Kind == SnackbarKind.Attention ? VisualStateNames.AttentionIcon
				: VisualStateNames.NoIcon);

			Go(SnackbarDuration.IsTimed(EffectiveDuration) ? VisualStateNames.Timed : VisualStateNames.Untimed);
			Go(IsHeld ? VisualStateNames.Held : VisualStateNames.Running);

			void Go(string state) => VisualStateManager.GoToState(this, state, useTransitions);
		}

		#region Countdown

		/// <summary>Starts the countdown from <see cref="Progress"/>. Does nothing for an untimed snackbar.</summary>
		public void StartCountdown()
		{
			StopCountdown();
			var duration = EffectiveDuration;
			if (!SnackbarDuration.IsTimed(duration)) return;

			_remaining = TimeSpan.FromTicks((long)(duration.Ticks * Math.Clamp(Progress, 0, 1)));
			_counting = true;
			if (!IsHeld) RunSegment();
		}

		/// <summary>Stops the countdown and leaves the track where it is.</summary>
		public void StopCountdown()
		{
			_counting = false;
			PauseSegment();
		}

		private void RunSegment()
		{
			if (_remaining <= TimeSpan.Zero)
			{
				OnTimerTick(null, null);
				return;
			}

			_segment.Restart();
			_timer.Interval = _remaining;
			_timer.Start();

			if (_drainScale != null)
			{
				var animation = new DoubleAnimation
				{
					From = _drainScale.ScaleX,
					To = 0,
					Duration = new Duration(_remaining),
				};
				Storyboard.SetTarget(animation, _drainScale);
				Storyboard.SetTargetProperty(animation, nameof(ScaleTransform.ScaleX));
				_drain = new Storyboard();
				_drain.Children.Add(animation);
				_drain.Begin();
			}
		}

		private void PauseSegment()
		{
			if (_timer.IsEnabled)
			{
				_timer.Stop();
				_remaining -= _segment.Elapsed;
			}
			_segment.Stop();

			if (_drain != null)
			{
				var x = _drainScale?.ScaleX ?? 0;
				_drain.Stop();
				_drain = null;
				if (_drainScale != null) _drainScale.ScaleX = x;
			}
		}

		private void OnTimerTick(object? sender, object? e)
		{
			_timer.Stop();
			_counting = false;
			_remaining = TimeSpan.Zero;
			if (_drainScale != null) _drainScale.ScaleX = 0;
			Expired?.Invoke(this, EventArgs.Empty);
		}

		private void ApplyProgress()
		{
			if (_drainScale != null && !_counting)
			{
				_drainScale.ScaleX = Math.Clamp(Progress, 0, 1);
			}
		}

		#endregion

		#region Hold (hover, focus, touch)

		private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
		{
			if (e.Pointer.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Mouse)
			{
				_touchDown = true;
				UpdateHold();
			}
		}

		private void OnPointerUp(object sender, PointerRoutedEventArgs e)
		{
			if (_touchDown)
			{
				_touchDown = false;
				UpdateHold();
			}
		}

		private void OnLostFocus(object sender, RoutedEventArgs e)
		{
			// Focus moving between the action and close buttons stays inside the snackbar.
			var focused = XamlRoot is { } root ? FocusManager.GetFocusedElement(root) as DependencyObject : null;
			_focusWithin = focused != null && IsDescendant(focused);
			UpdateHold();
		}

		private bool IsDescendant(DependencyObject element)
		{
			for (var current = element; current != null; current = VisualTreeHelper.GetParent(current))
			{
				if (ReferenceEquals(current, this)) return true;
			}
			return false;
		}

		private void UpdateHold() => IsHeld = _pointerOver || _focusWithin || _touchDown;

		private void OnIsHeldChanged(bool held)
		{
			if (_counting)
			{
				if (held) PauseSegment();
				else RunSegment();
			}
			if (_isReady)
			{
				VisualStateManager.GoToState(this, held ? VisualStateNames.Held : VisualStateNames.Running, true);
			}
		}

		#endregion

		private void OnActionButtonClick(object sender, RoutedEventArgs e)
		{
			if (ActionCommand?.CanExecute(ActionCommandParameter) == true)
			{
				ActionCommand.Execute(ActionCommandParameter);
			}
			ActionClicked?.Invoke(this, EventArgs.Empty);
		}

		private void OnDismissButtonClick(object sender, RoutedEventArgs e) => DismissClicked?.Invoke(this, EventArgs.Empty);

		private void OnKeyDown(object sender, KeyRoutedEventArgs e)
		{
			if (e.Key == Windows.System.VirtualKey.Escape)
			{
				DismissClicked?.Invoke(this, EventArgs.Empty);
				e.Handled = true;
			}
		}

		/// <summary>Asks screen readers to read the message (role=status, polite).</summary>
		internal void Announce()
		{
			if (FrameworkElementAutomationPeer.FromElement(this) is { } peer)
			{
				peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
			}
		}

		protected override AutomationPeer OnCreateAutomationPeer() => new SnackbarAutomationPeer(this);
	}

	/// <summary>
	/// Automation peer for the Snackbar control, exposing it as a status element for assistive technologies.
	/// </summary>
	internal class SnackbarAutomationPeer : FrameworkElementAutomationPeer
	{
		public SnackbarAutomationPeer(Snackbar owner) : base(owner) { }

		protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.StatusBar;

		protected override string GetClassNameCore() => nameof(Snackbar);

		protected override string GetNameCore()
			=> Owner is Snackbar snackbar ? snackbar.Message ?? string.Empty : base.GetNameCore();
	}
}
