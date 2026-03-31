namespace Rafatz.SumUpReceiptForwarder.Services;

/// <summary>
/// File-based receipt tracker that persists already-forwarded receipt IDs
/// as one ID per line in a plain text file on a Docker volume.
/// </summary>
public class FileReceiptTracker : IReceiptTracker
{
    private const string DefaultDataDirectory = "/app/data";
    private const string FileName = "sent-receipts.txt";

    private readonly string _dataDirectory;
    private readonly string _filePath;
    private readonly HashSet<string> _sentIds;
    private readonly Lock _lock = new();
    private readonly ILogger<FileReceiptTracker> _logger;

    public FileReceiptTracker(ILogger<FileReceiptTracker> logger, string? dataDirectory = null)
    {
        _logger = logger;
        _dataDirectory = dataDirectory ?? DefaultDataDirectory;
        _filePath = Path.Combine(_dataDirectory, FileName);
        _sentIds = LoadSentIds();
        _logger.LogInformation("Loaded {Count} previously sent receipt IDs from {FilePath}",
            _sentIds.Count, _filePath);
    }

    /// <inheritdoc />
    public bool IsAlreadySent(string receiptId)
    {
        lock (_lock)
        {
            return _sentIds.Contains(receiptId);
        }
    }

    /// <inheritdoc />
    public async Task MarkAsSentAsync(string receiptId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (!_sentIds.Add(receiptId))
            {
                return; // Already tracked
            }
        }

        Directory.CreateDirectory(_dataDirectory);
        await File.AppendAllLinesAsync(_filePath, [receiptId], cancellationToken);

        _logger.LogDebug("Marked receipt {ReceiptId} as sent", receiptId);
    }

    private HashSet<string> LoadSentIds()
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        var lines = File.ReadAllLines(_filePath)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToHashSet();

        return lines;
    }
}
