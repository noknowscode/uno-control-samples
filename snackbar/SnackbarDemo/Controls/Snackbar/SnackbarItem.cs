using System;
using System.Windows.Input;

namespace Uno.Toolkit.UI
{
	/// <summary>
	/// Describes the content and behavior of a single snackbar notification.
	/// </summary>
	public class SnackbarItem
	{
		/// <summary>Gets or sets the text message to display.</summary>
		public string Message { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets what the message is for (Confirm, Undo or Attention). The kind sets the default duration,
		/// the close button and the layout.
		/// </summary>
		public SnackbarKind Kind { get; set; }

		/// <summary>Gets or sets the label for the optional action button. When null or empty, no action button is shown.</summary>
		public string? ActionLabel { get; set; }

		/// <summary>Gets or sets the command to execute when the action button is clicked.</summary>
		public ICommand? ActionCommand { get; set; }

		/// <summary>Gets or sets the parameter to pass to <see cref="ActionCommand"/>.</summary>
		public object? ActionCommandParameter { get; set; }

		/// <summary>Gets or sets whether a close button is shown. Attention messages always show one.</summary>
		public bool ShowDismissButton { get; set; }

		/// <summary>
		/// Gets or sets whether the action sits below the message. Attention messages and messages longer than
		/// <see cref="Snackbar.MultiLineThreshold"/> characters put it there anyway.
		/// </summary>
		public bool IsActionOnNewLine { get; set; }

		/// <summary>
		/// Gets or sets an icon drawn before the message, in a 24×24 box (for example a stroked <c>Path</c>).
		/// Attention messages without an icon show a warning sign.
		/// </summary>
		public object? Icon { get; set; }

		/// <summary>
		/// Gets or sets the display duration. When null, the kind's default is used (<see cref="SnackbarDuration.For"/>).
		/// </summary>
		public TimeSpan? Duration { get; set; }
	}
}
