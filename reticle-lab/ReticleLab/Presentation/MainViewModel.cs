using System.Globalization;
using Microsoft.UI.Dispatching;
using ReticleLab.Controls;
using Windows.UI.ViewManagement;

namespace ReticleLab.Presentation;

/// <summary>
/// Simulates the prototype's workspace sync: Scan for 2.6 s while the files are counted, then Lock in bursts every
/// 200 ms until 100%, then Locked. The demo modes (Scan, Lock, Idle, Interrupt) jump straight to a phase.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private const int TotalFiles = 331;
    private const int TickMs = 200;
    private static readonly int[] Burst = [3, 6, 2, 8, 5, 1, 7, 4, 9, 2, 6, 3, 8, 5, 4, 7, 2, 6, 3, 5];

    // Uno implements AnimationsEnabled on Android only; desktop and WASM report motion on.
    private readonly UISettings _uiSettings = new();
    private readonly DispatcherQueueTimer _timer;
    private bool _autoLock;
    private int _burstIndex;
    private int _elapsedMs;

    public MainViewModel()
    {
        IsMotionReduced = !_uiSettings.AnimationsEnabled;
        _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(TickMs);
        _timer.Tick += (_, _) => OnTick();
        UpdateText();
    }

    public bool IsMotionReduced { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsScan), nameof(IsLock), nameof(IsIdle), nameof(CanInterrupt), nameof(RingIsActive), nameof(RingIsIndeterminate))]
    public partial ReticleState State { get; private set; } = ReticleState.Scanning;

    [ObservableProperty]
    public partial double Progress { get; private set; }

    [ObservableProperty]
    public partial string Telemetry { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string Detail { get; private set; } = string.Empty;

    public bool IsScan => State == ReticleState.Scanning;

    // The Lock pill stays on through Locked and an interrupted lock, as in the prototype.
    public bool IsLock => State is ReticleState.Locking or ReticleState.Locked or ReticleState.Interrupted;

    public bool IsIdle => State == ReticleState.Idle;

    /// <summary>The compact ProgressRings follow the loader: spin while scanning, show the value after, track only when idle.</summary>
    public bool RingIsActive => State != ReticleState.Idle;

    public bool RingIsIndeterminate => State == ReticleState.Scanning;

    public bool CanInterrupt => State is ReticleState.Scanning or ReticleState.Locking;

    /// <summary>Starts the full sequence (the prototype's "Run again"): Scan, then Lock, then Locked.</summary>
    [RelayCommand]
    public void RunAgain() => Start(autoLock: true);

    [RelayCommand]
    private void Scan() => Start(autoLock: false);

    [RelayCommand]
    private void Lock()
    {
        if (State == ReticleState.Locking)
        {
            return;
        }

        _timer.Stop();
        Progress = 0;
        _burstIndex = 0;
        _elapsedMs = 0;
        State = ReticleState.Locking;
        UpdateText();
        _timer.Start();
    }

    [RelayCommand]
    private void Idle()
    {
        _timer.Stop();
        Progress = 0;
        State = ReticleState.Idle;
        UpdateText();
    }

    /// <summary>Stops part-way and keeps the progress; the loader then offers Resume.</summary>
    [RelayCommand]
    private void Interrupt()
    {
        if (!CanInterrupt)
        {
            return;
        }

        _timer.Stop();
        State = ReticleState.Interrupted;
        UpdateText();
    }

    [RelayCommand]
    private void Resume()
    {
        if (State != ReticleState.Interrupted)
        {
            return;
        }

        State = ReticleState.Locking;
        UpdateText();
        _timer.Start();
    }

    /// <summary>Stops the simulation (the page calls it on Unloaded).</summary>
    public void Stop() => _timer.Stop();

    private void Start(bool autoLock)
    {
        _timer.Stop();
        _autoLock = autoLock;
        _burstIndex = 0;
        _elapsedMs = 0;
        Progress = 0;
        State = ReticleState.Scanning;
        UpdateText();
        _timer.Start();
    }

    private void OnTick()
    {
        _elapsedMs += TickMs;
        switch (State)
        {
            case ReticleState.Scanning:
                if (_autoLock && _elapsedMs >= 2600)
                {
                    State = ReticleState.Locking;
                }

                break;
            case ReticleState.Locking:
                Progress = Math.Min(100, Progress + Burst[_burstIndex++ % Burst.Length]);
                if (Progress >= 100)
                {
                    State = ReticleState.Locked;
                    _timer.Stop();
                }

                break;
            default:
                _timer.Stop();
                break;
        }

        UpdateText();
    }

    private void UpdateText()
    {
        var files = (int)Math.Round(TotalFiles * Progress / 100);
        var secondsLeft = Math.Max(0, (int)Math.Round((100 - Progress) / 20));
        var took = (_elapsedMs / 1000.0).ToString("0.0", CultureInfo.InvariantCulture);
        (Telemetry, Detail) = State switch
        {
            ReticleState.Scanning => ("COUNTING FILES", "Size unknown. The arc sweeps until the file count is in."),
            ReticleState.Locking => ($"{files}/{TotalFiles} · 14.2 MB/S", $"{files} of {TotalFiles} files · {secondsLeft} s left"),
            ReticleState.Locked => ($"{TotalFiles} FILES · {took} S", $"All {TotalFiles} files in {took} s. Nothing moves now."),
            ReticleState.Idle => ("WAITING", "Nothing queued."),
            _ => ($"{files}/{TotalFiles} · HELD", $"{files} of {TotalFiles} files. Progress is kept."),
        };
    }
}
