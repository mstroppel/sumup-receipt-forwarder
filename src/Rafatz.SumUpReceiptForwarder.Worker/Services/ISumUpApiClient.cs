using SumUp;

namespace Rafatz.SumUpReceiptForwarder.Services;

/// <summary>Client for interacting with the SumUp API.</summary>
public interface ISumUpApiClient
{
    /// <summary>
    /// Lists transaction history for the configured merchant account.
    /// </summary>
    /// <param name="oldestTime">Only return transactions created at or after this timestamp.</param>
    /// <param name="limit">Maximum number of results per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Transaction history items from the SumUp SDK.</returns>
    Task<IReadOnlyList<TransactionHistory>> ListTransactionsAsync(
        DateTimeOffset? oldestTime = null,
        int limit = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a receipt PDF for the given transaction.
    /// </summary>
    /// <param name="transactionId">The unique transaction ID (GUID).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The PDF content as a byte array.</returns>
    Task<byte[]> DownloadReceiptPdfAsync(
        string transactionId,
        CancellationToken cancellationToken = default);
}
