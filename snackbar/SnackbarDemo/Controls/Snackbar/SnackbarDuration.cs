using System;

namespace Uno.Toolkit.UI
{
	/// <summary>
	/// Provides predefined duration constants for snackbar display times.
	/// </summary>
	public static class SnackbarDuration
	{
		/// <summary>A short snackbar duration (4 seconds), the <see cref="SnackbarKind.Confirm"/> default.</summary>
		public static readonly TimeSpan Short = TimeSpan.FromSeconds(4);

		/// <summary>A long snackbar duration (7 seconds), the <see cref="SnackbarKind.Undo"/> default.</summary>
		public static readonly TimeSpan Long = TimeSpan.FromSeconds(7);

		/// <summary>An indefinite duration, the <see cref="SnackbarKind.Attention"/> default. The snackbar stays until dismissed.</summary>
		public static readonly TimeSpan Indefinite = TimeSpan.MaxValue;

		/// <summary>The default duration for a kind: Confirm 4 s, Undo 7 s, Attention indefinite.</summary>
		public static TimeSpan For(SnackbarKind kind) => kind switch
		{
			SnackbarKind.Undo => Long,
			SnackbarKind.Attention => Indefinite,
			_ => Short,
		};

		internal static bool IsTimed(TimeSpan duration) => duration > TimeSpan.Zero && duration != Indefinite;
	}
}
