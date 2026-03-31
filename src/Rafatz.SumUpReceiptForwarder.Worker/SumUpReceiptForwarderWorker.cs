using Microsoft.Extensions.Options;
using Rafatz.SumUpReceiptForwarder.Services;

namespace Rafatz.SumUpReceiptForwarder;

public class SumUpReceiptForwarderWorker(
    ILogger<SumUpReceiptForwarderWorker> _logger,
    IOptions<SumUpReceiptForwarderSettings> _options,
    ISumUpApiClient _sumUpApiClient,
    IEmailService _emailService,
    IReceiptTracker _receiptTracker) : BackgroundService
{
    private readonly SumUpReceiptForwarderSettings _settings = _options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SumUpReceiptForwarder worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessReceiptsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error during receipt processing cycle");
            }

            _logger.LogInformation("Waiting for {Minutes} minutes until next sync cycle", _settings.WorkerDelay);
            await Task.Delay(TimeSpan.FromMinutes(_settings.WorkerDelay), stoppingToken);
        }

        _logger.LogInformation("SumUpReceiptForwarder worker stopped");
    }

    private async Task ProcessReceiptsAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting receipt processing cycle");

        var transactions = await _sumUpApiClient.ListTransactionsAsync(
            limit: 50,
            cancellationToken: stoppingToken);

        _logger.LogInformation("Retrieved {Count} transactions from SumUp", transactions.Count);

        var forwarded = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var transaction in transactions)
        {
            if (stoppingToken.IsCancellationRequested) break;

            var receiptId = transaction.TransactionId ?? transaction.Id;
            if (string.IsNullOrWhiteSpace(receiptId))
            {
                _logger.LogWarning("Transaction has no usable ID, skipping. TransactionCode: {TransactionCode}",
                    transaction.TransactionCode);
                skipped++;
                continue;
            }

            if (_receiptTracker.IsAlreadySent(receiptId))
            {
                skipped++;
                continue;
            }

            try
            {
                var pdfBytes = await _sumUpApiClient.DownloadReceiptPdfAsync(receiptId, stoppingToken);

                var subject = $"SumUp Receipt - {transaction.TransactionCode} - {transaction.Amount} {transaction.Currency}";
                var body = $"""
                    Transaction Code: {transaction.TransactionCode}
                    Amount: {transaction.Amount} {transaction.Currency}
                    Date: {transaction.Timestamp}
                    Status: {transaction.Status}
                    Payment Type: {transaction.PaymentType}
                    """;
                var fileName = $"receipt-{transaction.TransactionCode}.pdf";

                await _emailService.SendReceiptAsync(subject, body, pdfBytes, fileName, stoppingToken);
                await _receiptTracker.MarkAsSentAsync(receiptId, stoppingToken);

                forwarded++;

                _logger.LogInformation(
                    "Forwarded receipt for transaction {TransactionCode} ({Amount} {Currency})",
                    transaction.TransactionCode, transaction.Amount, transaction.Currency);
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex,
                    "Failed to process receipt for transaction {TransactionCode} ({ReceiptId})",
                    transaction.TransactionCode, receiptId);
            }
        }

        _logger.LogInformation(
            "Receipt processing cycle complete. Forwarded: {Forwarded}, Skipped: {Skipped}, Failed: {Failed}",
            forwarded, skipped, failed);
    }
}
