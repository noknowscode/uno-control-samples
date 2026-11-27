using System;
using System.Windows.Input;

#if IS_WINUI
using Microsoft.UI.Xaml;
#else
using Windows.UI.Xaml;
#endif

namespace Uno.Toolkit.UI
{
	public partial class Snackbar
	{
		private static PropertyMetadata Layout(object defaultValue)
			=> new(defaultValue, (s, e) => ((Snackbar)s).OnLayoutPropertiesChanged());

		public static DependencyProperty MessageProperty { get; } = DependencyProperty.Register(
			nameof(Message), typeof(string), typeof(Snackbar), Layout(string.Empty));

		/// <summary>The message text.</summary>
		public string Message
		{
			get => (string)GetValue(MessageProperty);
			set => SetValue(MessageProperty, value);
		}

		public static DependencyProperty KindProperty { get; } = DependencyProperty.Register(
			nameof(Kind), typeof(SnackbarKind), typeof(Snackbar), Layout(SnackbarKind.Confirm));

		/// <summary>Confirm (4 s, no action), Undo (7 s, one action) or Attention (stays, icon, close button, action below).</summary>
		public SnackbarKind Kind
		{
			get => (SnackbarKind)GetValue(KindProperty);
			set => SetValue(KindProperty, value);
		}

		public static DependencyProperty ActionLabelProperty { get; } = DependencyProperty.Register(
			nameof(ActionLabel), typeof(string), typeof(Snackbar), Layout(null!));

		/// <summary>The action button label. No action button shows when it is null or empty.</summary>
		public string? ActionLabel
		{
			get => (string?)GetValue(ActionLabelProperty);
			set => SetValue(ActionLabelProperty, value);
		}

		public static DependencyProperty ActionCommandProperty { get; } = DependencyProperty.Register(
			nameof(ActionCommand), typeof(ICommand), typeof(Snackbar), new PropertyMetadata(default(ICommand)));

		public ICommand? ActionCommand
		{
			get => (ICommand?)GetValue(ActionCommandProperty);
			set => SetValue(ActionCommandProperty, value);
		}

		public static DependencyProperty ActionCommandParameterProperty { get; } = DependencyProperty.Register(
			nameof(ActionCommandParameter), typeof(object), typeof(Snackbar), new PropertyMetadata(default(object)));

		public object? ActionCommandParameter
		{
			get => GetValue(ActionCommandParameterProperty);
			set => SetValue(ActionCommandParameterProperty, value);
		}

		public static DependencyProperty ShowDismissButtonProperty { get; } = DependencyProperty.Register(
			nameof(ShowDismissButton), typeof(bool), typeof(Snackbar), Layout(false));

		/// <summary>Shows the close button. Attention snackbars always show it.</summary>
		public bool ShowDismissButton
		{
			get => (bool)GetValue(ShowDismissButtonProperty);
			set => SetValue(ShowDismissButtonProperty, value);
		}

		public static DependencyProperty IsActionOnNewLineProperty { get; } = DependencyProperty.Register(
			nameof(IsActionOnNewLine), typeof(bool), typeof(Snackbar), Layout(false));

		/// <summary>
		/// Puts the action below the message. Attention snackbars and messages longer than
		/// <see cref="MultiLineThreshold"/> characters put it there anyway.
		/// </summary>
		public bool IsActionOnNewLine
		{
			get => (bool)GetValue(IsActionOnNewLineProperty);
			set => SetValue(IsActionOnNewLineProperty, value);
		}

		public static DependencyProperty MultiLineThresholdProperty { get; } = DependencyProperty.Register(
			nameof(MultiLineThreshold), typeof(int), typeof(Snackbar), Layout(44));

		/// <summary>Message length (characters) above which the snackbar switches to the two-row layout. Default 44.</summary>
		public int MultiLineThreshold
		{
			get => (int)GetValue(MultiLineThresholdProperty);
			set => SetValue(MultiLineThresholdProperty, value);
		}

		public static DependencyProperty IconProperty { get; } = DependencyProperty.Register(
			nameof(Icon), typeof(object), typeof(Snackbar), Layout(null!));

		/// <summary>
		/// An icon before the message, laid out in a 24×24 box and shown at 18 px (for example a stroked <c>Path</c>).
		/// Attention snackbars without an icon show a warning sign.
		/// </summary>
		public object? Icon
		{
			get => GetValue(IconProperty);
			set => SetValue(IconProperty, value);
		}

		public static DependencyProperty DurationProperty { get; } = DependencyProperty.Register(
			nameof(Duration), typeof(TimeSpan?), typeof(Snackbar), Layout(null!));

		/// <summary>
		/// How long the countdown runs. When null, the kind's default applies (<see cref="SnackbarDuration.For"/>).
		/// A timed snackbar shows a 2 px track that drains as time runs out.
		/// </summary>
		public TimeSpan? Duration
		{
			get => (TimeSpan?)GetValue(DurationProperty);
			set => SetValue(DurationProperty, value);
		}

		public static DependencyProperty ProgressProperty { get; } = DependencyProperty.Register(
			nameof(Progress), typeof(double), typeof(Snackbar),
			new PropertyMetadata(1d, (s, e) => ((Snackbar)s).ApplyProgress()));

		/// <summary>
		/// The share of the duration left, from 1 (full track) to 0. The countdown starts from this value; set it to
		/// show a snackbar part-way through (a preview, a paused state).
		/// </summary>
		public double Progress
		{
			get => (double)GetValue(ProgressProperty);
			set => SetValue(ProgressProperty, value);
		}

		public static DependencyProperty IsHeldProperty { get; } = DependencyProperty.Register(
			nameof(IsHeld), typeof(bool), typeof(Snackbar),
			new PropertyMetadata(false, (s, e) => ((Snackbar)s).OnIsHeldChanged((bool)e.NewValue)));

		/// <summary>
		/// True while the countdown is paused: the pointer is over the snackbar, it has keyboard focus, or a finger
		/// is on it. The control sets it; an app can set it too to hold a message.
		/// </summary>
		public bool IsHeld
		{
			get => (bool)GetValue(IsHeldProperty);
			set => SetValue(IsHeldProperty, value);
		}

		/// <summary>The duration in effect: <see cref="Duration"/>, or the kind's default.</summary>
		public TimeSpan EffectiveDuration => Duration ?? SnackbarDuration.For(Kind);
	}
}
