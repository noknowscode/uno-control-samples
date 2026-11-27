namespace Liveline;

/// <summary>The stream states a <see cref="LivelineChart"/> shows.</summary>
public enum LivelineState
{
    /// <summary>Waiting for the first value: the axes and the tag's space are reserved, nothing is guessed.</summary>
    Connecting,
    /// <summary>One sample a second; the line glides between samples.</summary>
    Live,
    /// <summary>A pause was asked for: the chart stops at the next sample.</summary>
    Pausing,
    /// <summary>Stopped: the line greys out and the time of the pause is shown.</summary>
    Paused,
    /// <summary>The stream dropped: the line stops at the last sample and the gap since is hatched.</summary>
    Disconnected,
}
