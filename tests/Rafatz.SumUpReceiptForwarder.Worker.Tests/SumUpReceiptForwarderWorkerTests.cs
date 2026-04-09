using AutoFixture;
using AutoFixture.AutoMoq;
using Microsoft.Extensions.Options;
using Moq;
using Rafatz.SumUpReceiptForwarder.Services;
using SumUp;

namespace Rafatz.SumUpReceiptForwarder.Worker.Tests;

public class SumUpReceiptForwarderWorkerTests
{
    private readonly IFixture _fixture = new Fixture().Customize(new AutoMoqCustomization());
    private readonly Mock<IOptions<SumUpReceiptForwarderSettings>> _optionsMock;
    private readonly Mock<ISumUpApiClient> _apiClientMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<IReceiptTracker> _receiptTrackerMock;
    private readonly SumUpReceiptForwarderSettings _settings;
    private readonly SumUpReceiptForwarderWorker _sut;

    public SumUpReceiptForwarderWorkerTests()
    {
        _optionsMock = _fixture.Freeze<Mock<IOptions<SumUpReceiptForwarderSettings>>>();
        _apiClientMock = _fixture.Freeze<Mock<ISumUpApiClient>>();
        _emailServiceMock = _fixture.Freeze<Mock<IEmailService>>();
        _receiptTrackerMock = _fixture.Freeze<Mock<IReceiptTracker>>();

        _settings = new SumUpReceiptForwarderSettings
        {
            WorkerDelay = 1,
            SumUpAccountId = "test-account",
            SumUpApiKey = "test-key",
            SmtpHost = "localhost",
            SmtpPort = 587,
            SmtpUsername = "user",
            SmtpPassword = "pass",
            SmtpUseTls = false,
            SenderEmail = "sender@test.com",
            RecipientEmailCash = "recipient-cash@test.com",
            RecipientEmailCard = "recipient-card@test.com",
        };

        _optionsMock.Setup(x => x.Value).Returns(_settings);

        _sut = _fixture.Create<SumUpReceiptForwarderWorker>();
    }

    /// <summary>
    /// Starts the worker, waits briefly for it to process, then cancels.
    /// </summary>
    private async Task RunWorkerOneCycleAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var executeTask = _sut.StartAsync(cts.Token);
        await Task.Delay(500, ct);
        await cts.CancelAsync();

        try { await executeTask; } catch (OperationCanceledException) { }
    }

    [Fact]
    public async Task ExecuteAsync_WithNewTransactions_ForwardsReceipts()
    {
        // Arrange
        var transactions = new List<TransactionHistory>
        {
            new() { Id = "id-1", TransactionId = "tid-1", TransactionCode = "TX001", Amount = 10.50f, Currency = Currency.Eur, Status = "SUCCESSFUL" },
            new() { Id = "id-2", TransactionId = "tid-2", TransactionCode = "TX002", Amount = 25.00f, Currency = Currency.Eur, Status = "SUCCESSFUL" },
        };

        _apiClientMock
            .Setup(x => x.ListTransactionsAsync(It.IsAny<DateTimeOffset?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactions);

        _receiptTrackerMock
            .Setup(x => x.IsAlreadySent(It.IsAny<string>()))
            .Returns(false);

        _apiClientMock
            .Setup(x => x.DownloadReceiptPdfAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([0x25, 0x50, 0x44, 0x46]); // %PDF

        // Act
        await RunWorkerOneCycleAsync();

        // Assert
        _emailServiceMock.Verify(
            x => x.SendReceiptAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        _receiptTrackerMock.Verify(
            x => x.MarkAsSentAsync("tid-1", It.IsAny<CancellationToken>()), Times.Once);
        _receiptTrackerMock.Verify(
            x => x.MarkAsSentAsync("tid-2", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithAlreadySentReceipts_SkipsThem()
    {
        // Arrange
        var transactions = new List<TransactionHistory>
        {
            new() { Id = "id-1", TransactionId = "tid-1", TransactionCode = "TX001", Amount = 10.50f, Currency = Currency.Eur, Status = "SUCCESSFUL" },
            new() { Id = "id-2", TransactionId = "tid-2", TransactionCode = "TX002", Amount = 25.00f, Currency = Currency.Eur, Status = "SUCCESSFUL" },
        };

        _apiClientMock
            .Setup(x => x.ListTransactionsAsync(It.IsAny<DateTimeOffset?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactions);

        _receiptTrackerMock.Setup(x => x.IsAlreadySent("tid-1")).Returns(true);
        _receiptTrackerMock.Setup(x => x.IsAlreadySent("tid-2")).Returns(false);

        _apiClientMock
            .Setup(x => x.DownloadReceiptPdfAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([0x25, 0x50, 0x44, 0x46]);

        // Act
        await RunWorkerOneCycleAsync();

        // Assert - only the second receipt should be forwarded
        _emailServiceMock.Verify(
            x => x.SendReceiptAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _receiptTrackerMock.Verify(
            x => x.MarkAsSentAsync("tid-2", It.IsAny<CancellationToken>()), Times.Once);
        _receiptTrackerMock.Verify(
            x => x.MarkAsSentAsync("tid-1", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenReceiptDownloadFails_ContinuesWithOtherReceipts()
    {
        // Arrange
        var transactions = new List<TransactionHistory>
        {
            new() { Id = "id-1", TransactionId = "tid-1", TransactionCode = "TX001", Amount = 10.50f, Currency = Currency.Eur },
            new() { Id = "id-2", TransactionId = "tid-2", TransactionCode = "TX002", Amount = 25.00f, Currency = Currency.Eur },
        };

        _apiClientMock
            .Setup(x => x.ListTransactionsAsync(It.IsAny<DateTimeOffset?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactions);

        _receiptTrackerMock.Setup(x => x.IsAlreadySent(It.IsAny<string>())).Returns(false);

        // First download fails, second succeeds
        _apiClientMock
            .Setup(x => x.DownloadReceiptPdfAsync("tid-1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Download failed"));

        _apiClientMock
            .Setup(x => x.DownloadReceiptPdfAsync("tid-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync([0x25, 0x50, 0x44, 0x46]);

        // Act
        await RunWorkerOneCycleAsync();

        // Assert - second receipt should still be forwarded despite first failing
        _emailServiceMock.Verify(
            x => x.SendReceiptAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _receiptTrackerMock.Verify(
            x => x.MarkAsSentAsync("tid-2", It.IsAny<CancellationToken>()), Times.Once);
        _receiptTrackerMock.Verify(
            x => x.MarkAsSentAsync("tid-1", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoTransactions_DoesNotSendEmails()
    {
        // Arrange
        _apiClientMock
            .Setup(x => x.ListTransactionsAsync(It.IsAny<DateTimeOffset?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TransactionHistory>());

        // Act
        await RunWorkerOneCycleAsync();

        // Assert
        _emailServiceMock.Verify(
            x => x.SendReceiptAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_SendsCashAndCardToDifferentRecipients()
    {
        // Arrange
        var transactions = new List<TransactionHistory>
        {
            new() { Id = "id-1", TransactionId = "tid-1", TransactionCode = "TX001", Amount = 10.50f, Currency = Currency.Eur, PaymentType = ResolvePaymentTypeByKeyword("cash") },
            new() { Id = "id-2", TransactionId = "tid-2", TransactionCode = "TX002", Amount = 25.00f, Currency = Currency.Eur, PaymentType = ResolveNonCashPaymentType() },
        };

        _apiClientMock
            .Setup(x => x.ListTransactionsAsync(It.IsAny<DateTimeOffset?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactions);

        _receiptTrackerMock.Setup(x => x.IsAlreadySent(It.IsAny<string>())).Returns(false);

        _apiClientMock
            .Setup(x => x.DownloadReceiptPdfAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([0x25, 0x50, 0x44, 0x46]);

        // Act
        await RunWorkerOneCycleAsync();

        // Assert
        _emailServiceMock.Verify(
            x => x.SendReceiptAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                "recipient-cash@test.com",
                It.IsAny<byte[]>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _emailServiceMock.Verify(
            x => x.SendReceiptAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                "recipient-card@test.com",
                It.IsAny<byte[]>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTransactionHasNoId_SkipsIt()
    {
        // Arrange
        var transactions = new List<TransactionHistory>
        {
            new() { Id = null, TransactionId = null, TransactionCode = "TX-NO-ID" },
        };

        _apiClientMock
            .Setup(x => x.ListTransactionsAsync(It.IsAny<DateTimeOffset?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactions);

        // Act
        await RunWorkerOneCycleAsync();

        // Assert
        _apiClientMock.Verify(
            x => x.DownloadReceiptPdfAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_UsesTransactionIdOverId()
    {
        // Arrange - TransactionId should be preferred over Id
        var transactions = new List<TransactionHistory>
        {
            new() { Id = "generic-id", TransactionId = "specific-tid", TransactionCode = "TX001", Amount = 5f, Currency = Currency.Eur },
        };

        _apiClientMock
            .Setup(x => x.ListTransactionsAsync(It.IsAny<DateTimeOffset?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactions);

        _receiptTrackerMock.Setup(x => x.IsAlreadySent(It.IsAny<string>())).Returns(false);

        _apiClientMock
            .Setup(x => x.DownloadReceiptPdfAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([0x25, 0x50, 0x44, 0x46]);

        // Act
        await RunWorkerOneCycleAsync();

        // Assert - should use TransactionId, not Id
        _receiptTrackerMock.Verify(x => x.IsAlreadySent("specific-tid"), Times.Once);
        _apiClientMock.Verify(x => x.DownloadReceiptPdfAsync("specific-tid", It.IsAny<CancellationToken>()), Times.Once);
        _receiptTrackerMock.Verify(x => x.MarkAsSentAsync("specific-tid", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_FallsBackToIdWhenTransactionIdIsNull()
    {
        // Arrange
        var transactions = new List<TransactionHistory>
        {
            new() { Id = "fallback-id", TransactionId = null, TransactionCode = "TX001", Amount = 5f, Currency = Currency.Eur },
        };

        _apiClientMock
            .Setup(x => x.ListTransactionsAsync(It.IsAny<DateTimeOffset?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactions);

        _receiptTrackerMock.Setup(x => x.IsAlreadySent(It.IsAny<string>())).Returns(false);

        _apiClientMock
            .Setup(x => x.DownloadReceiptPdfAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([0x25, 0x50, 0x44, 0x46]);

        // Act
        await RunWorkerOneCycleAsync();

        // Assert
        _receiptTrackerMock.Verify(x => x.IsAlreadySent("fallback-id"), Times.Once);
        _apiClientMock.Verify(x => x.DownloadReceiptPdfAsync("fallback-id", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ExtractsIdFromClientTransactionId()
    {
        // Arrange
        var transactions = new List<TransactionHistory>
        {
            new()
            {
                Id = "wrong-id",
                TransactionId = "also-wrong-id",
                ClientTransactionId = "urn:sumup:pos:sale:MFQWVP79:2d3f18f7-79f9-4056-9976-6e326b0ab36d:1774878792483",
                TransactionCode = "TX001",
                Amount = 5f,
                Currency = Currency.Eur
            },
        };

        var expectedId = "2d3f18f7-79f9-4056-9976-6e326b0ab36d";

        _apiClientMock
            .Setup(x => x.ListTransactionsAsync(It.IsAny<DateTimeOffset?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactions);

        _receiptTrackerMock.Setup(x => x.IsAlreadySent(It.IsAny<string>())).Returns(false);

        _apiClientMock
            .Setup(x => x.DownloadReceiptPdfAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([0x25, 0x50, 0x44, 0x46]);

        // Act
        await RunWorkerOneCycleAsync();

        // Assert
        _receiptTrackerMock.Verify(x => x.IsAlreadySent(expectedId), Times.Once);
        _apiClientMock.Verify(x => x.DownloadReceiptPdfAsync(expectedId, It.IsAny<CancellationToken>()), Times.Once);
        _receiptTrackerMock.Verify(x => x.MarkAsSentAsync(expectedId, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static PaymentType ResolvePaymentTypeByKeyword(string keyword)
    {
        foreach (var value in Enum.GetValues<PaymentType>())
        {
            if (value.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        throw new InvalidOperationException($"Unable to resolve SumUp payment type for keyword '{keyword}'.");
    }

    private static PaymentType ResolveNonCashPaymentType()
    {
        foreach (var value in Enum.GetValues<PaymentType>())
        {
            if (!value.ToString().Contains("cash", StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        throw new InvalidOperationException("Unable to resolve non-cash SumUp payment type.");
    }
}
