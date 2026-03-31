namespace Rafatz.SumUpReceiptForwarder.Services;

/// <summary>Tracks which receipts have already been forwarded to avoid duplicates.</summary>
public interface IReceiptTracker
{
    /// <summary>
    /// Checks whether a receipt has already been sent.
    /// </summary>
    /// <param name="receiptId">The unique receipt/transaction identifier.</param>
    /// <returns>True if the receipt was already forwarded.</returns>
    bool IsAlreadySent(string receiptId);

    /// <summary>
    /// Marks a receipt as sent so it will not be forwarded again.
    /// </summary>
    /// <param name="receiptId">The unique receipt/transaction identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MarkAsSentAsync(string receiptId, CancellationToken cancellationToken = default);
}
