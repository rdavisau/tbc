namespace Tbc.Host.Components.TargetClient;

public enum CanonicalChannelState
{
    /// <summary>
    /// Channel is idle
    /// </summary>
    Idle,

    /// <summary>
    /// Channel is connecting
    /// </summary>
    Connecting,

    /// <summary>
    /// Channel is ready for work
    /// </summary>
    Ready,

    /// <summary>
    /// Channel has seen a failure but expects to recover
    /// </summary>
    TransientFailure,

    /// <summary>
    /// Channel has seen a failure that it cannot recover from
    /// </summary>
    Shutdown
}