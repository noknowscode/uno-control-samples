namespace Uno.Toolkit.UI
{
	/// <summary>
	/// What a snackbar is for. The kind sets the default duration, whether a close button shows and the layout.
	/// </summary>
	public enum SnackbarKind
	{
		/// <summary>A quiet success: 4 s, no action.</summary>
		Confirm,

		/// <summary>Something can be reversed: 7 s, one action.</summary>
		Undo,

		/// <summary>Needs a decision: stays until dismissed, shows a warning icon and a close button, the action sits below.</summary>
		Attention,
	}
}
