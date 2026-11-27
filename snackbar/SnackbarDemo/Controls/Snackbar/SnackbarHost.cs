using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;

#if IS_WINUI
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
#else
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
#endif

namespace Uno.Toolkit.UI
{
	[TemplatePart(Name = TemplateParts.SnackbarPresenterName, Type = typeof(ContentPresenter))]
	[TemplateVisualState(GroupName = VisualStateNames.GroupName, Name = VisualStateNames.Hidden)]
	[TemplateVisualState(GroupName = VisualStateNames.GroupName, Name = VisualStateNames.Visible)]
	[TemplateVisualState(GroupName = VisualStateNames.GroupName, Name = VisualStateNames.Replacing)]
	public partial class SnackbarHost : ContentControl
	{
		internal static class TemplateParts
		{
			public const string SnackbarPresenterName = "PART_SnackbarPresenter";
		}

		private class VisualStateNames
		{
			public const string GroupName = "DisplayStates";
			public const string Hidden = nameof(Hidden);
			public const string Visible = nameof(Visible);
			public const string Replacing = nameof(Replacing);
		}

		private const double SwipeDismissThresholdRatio = 1.0 / 3;

		// Motion from the design: 220 ms rise in (12 px, cubic-bezier(.2,.8,.2,1)), 160 ms fade out (ease).
		// A replacing message waits for the exit, so the next one shows 160 ms later and never stacks.
		private static readonly TimeSpan EnterAnimationDuration = TimeSpan.FromMilliseconds(220);
		private static readonly TimeSpan ExitAnimationDuration = TimeSpan.FromMilliseconds(160);
		private const double RiseOffset = 12;

		private ContentPresenter? _snackbarPresenter;
		private DispatcherCompat _dispatcher;
		private Storyboard _enterStoryboard = new Storyboard();
		private Storyboard _exitStoryboard = new Storyboard();
		private TranslateTransform? _translateTransform;

		private readonly Queue<PendingSnackbar> _queue = new();
		private PendingSnackbar? _current;
		private bool _isReady;
		private bool _isAnimating;
		private bool _isSwiping;

		internal Storyboard EnterStoryboard => _enterStoryboard;
		internal Storyboard ExitStoryboard => _exitStoryboard;

		public event EventHandler<SnackbarItem>? SnackbarOpened;
		public event EventHandler<SnackbarItem>? SnackbarClosed;

		public SnackbarHost()
		{
			DefaultStyleKey = typeof(SnackbarHost);
			_dispatcher = this.GetDispatcherCompat();
			Loaded += OnLoaded;
			Unloaded += OnUnloaded;
		}

		private void OnLoaded(object sender, RoutedEventArgs e)
		{
			// Re-parenting fires Unloaded then Loaded without a new template: become ready again.
			if (!_isReady && _snackbarPresenter != null)
			{
				_isReady = true;
				ProcessQueue();
			}
		}

		private void OnUnloaded(object sender, RoutedEventArgs e)
		{
			StopRunningAnimations();

			if (_current != null)
			{
				_current.CompletionSource.TrySetResult(SnackbarDismissReason.Dismiss);
				_current = null;
			}

			while (_queue.TryDequeue(out var pending))
			{
				pending.CompletionSource.TrySetResult(SnackbarDismissReason.Replaced);
			}

			IsShowing = false;
			OccupiedHeight = 0;
			_isReady = false;
		}

		protected override void OnApplyTemplate()
		{
			StopRunningAnimations();
			base.OnApplyTemplate();

			_snackbarPresenter = GetTemplateChild(TemplateParts.SnackbarPresenterName) as ContentPresenter;

			if (_snackbarPresenter != null)
			{
				_snackbarPresenter.SizeChanged -= OnPresenterSizeChanged;
				_snackbarPresenter.SizeChanged += OnPresenterSizeChanged;
				_snackbarPresenter.RenderTransform = _translateTransform = new TranslateTransform();
				SetupAnimations();
			}

			_isReady = true;

			// ShowAsync calls made before the template was applied are waiting in the queue.
			if (_queue.Count > 0)
			{
				ProcessQueue();
			}
		}

		#region Public API

		public Task<SnackbarDismissReason> ShowAsync(SnackbarItem item)
		{
			if (item == null) throw new ArgumentNullException(nameof(item));

			var pending = new PendingSnackbar(item);
			EnqueueItem(pending);
			ProcessQueue();

			return pending.CompletionSource.Task;
		}

		public Task<SnackbarDismissReason> ShowAsync(
			string message,
			string? actionLabel = null,
			ICommand? actionCommand = null,
			TimeSpan? duration = null,
			SnackbarKind? kind = null)
		{
			return ShowAsync(new SnackbarItem
			{
				Message = message,
				Kind = kind ?? (string.IsNullOrEmpty(actionLabel) ? SnackbarKind.Confirm : SnackbarKind.Undo),
				ActionLabel = actionLabel,
				ActionCommand = actionCommand,
				Duration = duration,
			});
		}

		public void Dismiss()
		{
			DismissCurrent(SnackbarDismissReason.Dismiss);
		}

		#endregion

		#region Queue Management

		private void EnqueueItem(PendingSnackbar pending)
		{
			_queue.Enqueue(pending);

			while (_queue.Count > MaxQueueSize)
			{
				if (_queue.TryDequeue(out var dropped))
				{
					dropped.CompletionSource.TrySetResult(SnackbarDismissReason.Replaced);
				}
			}
		}

		private void ProcessQueue()
		{
			if (_isAnimating || !_isReady) return;

			if (_current != null)
			{
				DismissCurrent(SnackbarDismissReason.Replaced);
				return;
			}

			if (!_queue.TryDequeue(out var next)) return;

			_current = next;
			ShowCurrentSnackbar();
		}

		#endregion

		#region Display Logic

		private void ShowCurrentSnackbar()
		{
			if (_current == null || _snackbarPresenter == null) return;

			var item = _current.Item;

			var snackbar = new Snackbar
			{
				Message = item.Message,
				Kind = item.Kind,
				ActionLabel = item.ActionLabel ?? string.Empty,
				ActionCommand = item.ActionCommand,
				ActionCommandParameter = item.ActionCommandParameter,
				ShowDismissButton = item.ShowDismissButton,
				IsActionOnNewLine = item.IsActionOnNewLine,
				Icon = item.Icon,
				Duration = item.Duration,
			};

			if (SnackbarStyle != null)
			{
				snackbar.Style = SnackbarStyle;
			}

			snackbar.ActionClicked += OnSnackbarActionClicked;
			snackbar.DismissClicked += OnSnackbarDismissClicked;
			snackbar.Expired += OnSnackbarExpired;

			if (IsSwipeToDismissEnabled)
			{
				_snackbarPresenter.ManipulationMode = ManipulationModes.TranslateX;
				_snackbarPresenter.ManipulationDelta += OnManipulationDelta;
				_snackbarPresenter.ManipulationCompleted += OnManipulationCompleted;
			}

			_snackbarPresenter.Content = snackbar;
			_snackbarPresenter.Visibility = Visibility.Visible;
			IsShowing = true;
			IsReplacing = false;
			// SizeChanged only fires when the height changes; a message as tall as the last one keeps that height.
			if (_snackbarPresenter.ActualHeight > 0)
			{
				OccupiedHeight = _snackbarPresenter.ActualHeight + _snackbarPresenter.Margin.Bottom;
			}
			VisualStateManager.GoToState(this, VisualStateNames.Visible, true);
			snackbar.Announce();

			PlayEnterAnimation(() =>
			{
				SnackbarOpened?.Invoke(this, item);
				snackbar.StartCountdown();

				// A ShowAsync call made during the enter animation was queued; it replaces this one now,
				// as it would have if it had arrived a moment later.
				if (_queue.Count > 0)
				{
					ProcessQueue();
				}
			});
		}

		private void DismissCurrent(SnackbarDismissReason reason)
		{
			if (_current == null) return;

			StopRunningAnimations();
			(_snackbarPresenter?.Content as Snackbar)?.StopCountdown();

			var current = _current;
			_current = null;

			// Replace hand-off: the next message waits for this one's 160 ms exit; the two never stack.
			IsReplacing = reason == SnackbarDismissReason.Replaced;
			VisualStateManager.GoToState(this, IsReplacing ? VisualStateNames.Replacing : VisualStateNames.Hidden, true);

			PlayExitAnimation(() =>
			{
				CleanupSnackbar();
				current.CompletionSource.TrySetResult(reason);
				SnackbarClosed?.Invoke(this, current.Item);

				_dispatcher.Invoke(() =>
				{
					ProcessQueue();
					if (_current == null && IsReplacing)
					{
						IsReplacing = false;
						VisualStateManager.GoToState(this, VisualStateNames.Hidden, true);
					}
				});
			});
		}

		// The message's real height (one line, two lines, an action row) decides how much of the bottom it covers.
		private void OnPresenterSizeChanged(object sender, SizeChangedEventArgs e)
		{
			if (IsShowing && _snackbarPresenter != null)
			{
				OccupiedHeight = e.NewSize.Height + _snackbarPresenter.Margin.Bottom;
			}
		}

		private void CleanupSnackbar()
		{
			if (_snackbarPresenter != null)
			{
				if (_snackbarPresenter.Content is Snackbar snackbar)
				{
					snackbar.ActionClicked -= OnSnackbarActionClicked;
					snackbar.DismissClicked -= OnSnackbarDismissClicked;
					snackbar.Expired -= OnSnackbarExpired;
				}

				_snackbarPresenter.ManipulationDelta -= OnManipulationDelta;
				_snackbarPresenter.ManipulationCompleted -= OnManipulationCompleted;
				_snackbarPresenter.Content = null;
				_snackbarPresenter.Visibility = Visibility.Collapsed;
			}

			IsShowing = false;
			OccupiedHeight = 0;

			if (_translateTransform != null)
			{
				_translateTransform.X = 0;
				_translateTransform.Y = 0;
			}
		}

		#endregion

		#region Animations

		private void SetupAnimations()
		{
			if (_translateTransform == null || _snackbarPresenter == null) return;

			var rise = new KeySpline { ControlPoint1 = new Windows.Foundation.Point(0.2, 0.8), ControlPoint2 = new Windows.Foundation.Point(0.2, 1) };
			var ease = new KeySpline { ControlPoint1 = new Windows.Foundation.Point(0.25, 0.1), ControlPoint2 = new Windows.Foundation.Point(0.25, 1) };

			_enterStoryboard = new Storyboard();
			_enterStoryboard.Children.Add(KeyFrames(_translateTransform, nameof(TranslateTransform.Y), RiseOffset, 0, EnterAnimationDuration, rise));
			_enterStoryboard.Children.Add(KeyFrames(_snackbarPresenter, nameof(UIElement.Opacity), 0, 1, EnterAnimationDuration, ease));

			_exitStoryboard = new Storyboard();
			_exitStoryboard.Children.Add(KeyFrames(_translateTransform, nameof(TranslateTransform.Y), 0, RiseOffset, ExitAnimationDuration, ease));
			_exitStoryboard.Children.Add(KeyFrames(_snackbarPresenter, nameof(UIElement.Opacity), 1, 0, ExitAnimationDuration, ease));
		}

		private static DoubleAnimationUsingKeyFrames KeyFrames(DependencyObject target, string property, double from, double to, TimeSpan duration, KeySpline spline)
		{
			var animation = new DoubleAnimationUsingKeyFrames();
			animation.KeyFrames.Add(new DiscreteDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.Zero), Value = from });
			animation.KeyFrames.Add(new SplineDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(duration), Value = to, KeySpline = spline });
			Storyboard.SetTarget(animation, target);
			Storyboard.SetTargetProperty(animation, property);
			return animation;
		}

		private void StopRunningAnimations()
		{
			StopStoryboard(_enterStoryboard);
			StopStoryboard(_exitStoryboard);
			_isAnimating = false;
		}

		private void StopStoryboard(Storyboard storyboard)
		{
			if (storyboard.GetCurrentState() != ClockState.Stopped)
			{
				storyboard.Pause();
				var currentY = _translateTransform?.Y ?? 0;
				var currentOpacity = _snackbarPresenter?.Opacity ?? 1;

				storyboard.Stop();

				if (_translateTransform != null) _translateTransform.Y = currentY;
				if (_snackbarPresenter != null) _snackbarPresenter.Opacity = currentOpacity;
			}
		}

		private void PlayEnterAnimation(Action? onCompleted = null)
		{
			_isAnimating = true;
			_enterStoryboard.Completed += OnEnterCompleted;
			_enterStoryboard.Begin();

			void OnEnterCompleted(object? sender, object e)
			{
				_enterStoryboard.Completed -= OnEnterCompleted;
				_isAnimating = false;
				onCompleted?.Invoke();
			}
		}

		private void PlayExitAnimation(Action? onCompleted = null)
		{
			_isAnimating = true;
			_exitStoryboard.Completed += OnExitCompleted;
			_exitStoryboard.Begin();

			void OnExitCompleted(object? sender, object e)
			{
				_exitStoryboard.Completed -= OnExitCompleted;
				_isAnimating = false;
				onCompleted?.Invoke();
			}
		}

		#endregion

		#region Swipe to Dismiss

		private void OnManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
		{
			if (_translateTransform == null) return;

			_translateTransform.X += e.Delta.Translation.X;
			_isSwiping = true;

			if (_snackbarPresenter != null)
			{
				var presenterWidth = _snackbarPresenter.ActualWidth > 0 ? _snackbarPresenter.ActualWidth : 300;
				var progress = Math.Abs(_translateTransform.X) / presenterWidth;
				_snackbarPresenter.Opacity = Math.Max(0, 1 - progress);
			}
		}

		private void OnManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
		{
			if (_translateTransform == null || !_isSwiping) return;

			_isSwiping = false;
			var threshold = (_snackbarPresenter?.ActualWidth ?? 300) * SwipeDismissThresholdRatio;

			if (Math.Abs(_translateTransform.X) > threshold)
			{
				DismissCurrent(SnackbarDismissReason.Dismiss);
			}
			else
			{
				SnapBackTranslateX();
			}
		}

		private void SnapBackTranslateX()
		{
			if (_translateTransform == null) return;

			var snapBack = new DoubleAnimation
			{
				To = 0,
				Duration = new Duration(TimeSpan.FromMilliseconds(150)),
				EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
			};
			Storyboard.SetTarget(snapBack, _translateTransform);
			Storyboard.SetTargetProperty(snapBack, nameof(TranslateTransform.X));

			var opacityRestore = new DoubleAnimation
			{
				To = 1,
				Duration = new Duration(TimeSpan.FromMilliseconds(150)),
			};
			Storyboard.SetTarget(opacityRestore, _snackbarPresenter!);
			Storyboard.SetTargetProperty(opacityRestore, nameof(UIElement.Opacity));

			var sb = new Storyboard();
			sb.Children.Add(snapBack);
			sb.Children.Add(opacityRestore);
			sb.Begin();
		}

		#endregion

		#region Event Handlers

		private void OnSnackbarActionClicked(object? sender, EventArgs e)
		{
			DismissCurrent(SnackbarDismissReason.Action);
		}

		private void OnSnackbarDismissClicked(object? sender, EventArgs e)
		{
			DismissCurrent(SnackbarDismissReason.Dismiss);
		}

		private void OnSnackbarExpired(object? sender, EventArgs e)
		{
			DismissCurrent(SnackbarDismissReason.Timeout);
		}

		#endregion

		#region Internal Types

		private class PendingSnackbar
		{
			public SnackbarItem Item { get; }
			public TaskCompletionSource<SnackbarDismissReason> CompletionSource { get; } = new();

			public PendingSnackbar(SnackbarItem item)
			{
				Item = item;
			}
		}

		#endregion
	}
}
