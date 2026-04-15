using Microsoft.Extensions.Options;
using SumUp;

namespace Rafatz.SumUpReceiptForwarder.Services;

/// <summary>Client for interacting with the SumUp API using the official SDK.</summary>
public class SumUpApiClient(
    SumUpClient sumUpClient,
    IHttpClientFactory httpClientFactory,
    IOptions<SumUpReceiptForwarderSettings> options,
    ILogger<SumUpApiClient> logger) : ISumUpApiClient
{
    public const string ReceiptHttpClientName = "SumUpReceipt";

    private readonly SumUpReceiptForwarderSettings _settings = options.Value;

    /// <inheritdoc />
    public async Task<IReadOnlyList<TransactionHistory>> ListTransactionsAsync(
        DateTimeOffset? oldestTime = null,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Fetching up to {Limit} transactions from SumUp API (oldest: {OldestTime})",
            limit, oldestTime);

        var response = await sumUpClient.Transactions.ListAsync(
            merchantCode: _settings.SumUpAccountId,
            statuses: ["SUCCESSFUL"],
            oldestTime: oldestTime,
            limit: limit,
            order: "descending",
            cancellationToken: cancellationToken);

        var items = response.Data?.Items?.ToList() ?? [];

        logger.LogInformation("Fetched {Count} transactions from SumUp API", items.Count);

        return items;
    }

    /// <summary>Maximum allowed size for a downloaded receipt PDF (10 MB).</summary>
    public const long MaxReceiptPdfSizeBytes = 10 * 1024 * 1024;

    /// <inheritdoc />
    public async Task<byte[]> DownloadReceiptPdfAsync(
        string transactionId,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(transactionId, out _))
        {
            throw new ArgumentException(
                $"Transaction ID '{transactionId}' is not a valid GUID and cannot be used in a receipt URL.",
                nameof(transactionId));
        }

        var client = httpClientFactory.CreateClient(ReceiptHttpClientName);

        var url = $"pos/public/v1/{Uri.EscapeDataString(_settings.SumUpAccountId)}/receipt/{Uri.EscapeDataString(transactionId)}?format=pdf";

        logger.LogDebug("Downloading receipt PDF for transaction {TransactionId}", transactionId);

        var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength.HasValue && contentLength.Value > MaxReceiptPdfSizeBytes)
        {
            throw new InvalidOperationException(
                $"Receipt PDF for transaction {transactionId} exceeds maximum allowed size " +
                $"({contentLength.Value} bytes > {MaxReceiptPdfSizeBytes} bytes).");
        }

        var pdfBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        if (pdfBytes.Length > MaxReceiptPdfSizeBytes)
        {
            throw new InvalidOperationException(
                $"Receipt PDF for transaction {transactionId} exceeds maximum allowed size " +
                $"({pdfBytes.Length} bytes > {MaxReceiptPdfSizeBytes} bytes).");
        }

        logger.LogInformation("Downloaded receipt PDF for transaction {TransactionId} ({Bytes} bytes)",
            transactionId, pdfBytes.Length);

        return pdfBytes;
    }
}
