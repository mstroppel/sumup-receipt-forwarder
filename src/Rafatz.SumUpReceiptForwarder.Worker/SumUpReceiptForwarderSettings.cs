namespace Rafatz.SumUpReceiptForwarder;

public class SumUpReceiptForwarderSettings
{
    /// <summary>How often the worker runs the sync, in minutes.</summary>
    public required int SyncIntervalMinutes { get; init; }
}
