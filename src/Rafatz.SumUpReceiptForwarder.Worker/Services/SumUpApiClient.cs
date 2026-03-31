using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Rafatz.SumUpReceiptForwarder.Models;

namespace Rafatz.SumUpReceiptForwarder.Services;

/// <summary>Client for interacting with the SumUp API.</summary>
public class SumUpApiClient(
    IHttpClientFactory httpClientFactory,
    IOptions<SumUpReceiptForwarderSettings> options,
    ILogger<SumUpApiClient> logger) : ISumUpApiClient
{
    public const string ApiHttpClientName = "SumUpApi";
    public const string ReceiptHttpClientName = "SumUpReceipt";

    private readonly SumUpReceiptForwarderSettings _settings = options.Value;

    /// <inheritdoc />
    public async Task<TransactionHistoryResponse> ListTransactionsAsync(
        DateTime? oldestTime = null,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(ApiHttpClientName);

        var url = $"v2.1/merchants/{_settings.SumUpAccountId}/transactions/history?limit={limit}&order=descending&statuses[]=SUCCESSFUL";

        if (oldestTime.HasValue)
        {
            url += $"&oldest_time={oldestTime.Value:yyyy-MM-ddTHH:mm:ss.fffZ}";
        }

        logger.LogDebug("Fetching transactions from {Url}", url);

        var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<TransactionHistoryResponse>(cancellationToken);

        if (result is null)
        {
            throw new InvalidOperationException("Failed to deserialize transaction history response from SumUp API.");
        }

        logger.LogInformation("Fetched {Count} transactions from SumUp API", result.Items.Count);

        return result;
    }

    /// <inheritdoc />
    public async Task<byte[]> DownloadReceiptPdfAsync(
        string transactionId,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(ReceiptHttpClientName);

        var url = $"pos/public/v1/{_settings.SumUpAccountId}/receipt/{transactionId}?format=pdf";

        logger.LogDebug("Downloading receipt PDF for transaction {TransactionId}", transactionId);

        var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var pdfBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        logger.LogInformation("Downloaded receipt PDF for transaction {TransactionId} ({Bytes} bytes)",
            transactionId, pdfBytes.Length);

        return pdfBytes;
    }
}
