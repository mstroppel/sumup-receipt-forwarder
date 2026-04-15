using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Rafatz.SumUpReceiptForwarder.Services;
using SumUp;

namespace Rafatz.SumUpReceiptForwarder.Worker.Tests;

public class SumUpApiClientTests
{
    private readonly SumUpReceiptForwarderSettings _settings = new()
    {
        WorkerDelay = 1,
        SumUpAccountId = "ACCOUNT123",
        SumUpApiKey = "test-key",
        SmtpHost = "localhost",
        SmtpPort = 587,
        SmtpUsername = "user",
        SmtpPassword = "pass",
        SmtpUseTls = true,
        SenderEmail = "sender@test.com",
        RecipientEmailCash = "cash@test.com",
        RecipientEmailCard = "card@test.com",
    };

    private SumUpApiClient CreateSut(HttpMessageHandler handler)
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(f => f.CreateClient(SumUpApiClient.ReceiptHttpClientName))
            .Returns(new HttpClient(handler) { BaseAddress = new Uri("https://sales-receipt.sumup.com/") });

        return new SumUpApiClient(
            new SumUpClient(new SumUpClientOptions { AccessToken = "test" }),
            httpClientFactory.Object,
            Options.Create(_settings),
            new Mock<ILogger<SumUpApiClient>>().Object);
    }

    // -------------------------------------------------------------------------
    // H2: transaction ID validation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DownloadReceiptPdfAsync_WithValidGuid_DoesNotThrow()
    {
        var ct = TestContext.Current.CancellationToken;
        var guid = Guid.NewGuid().ToString();
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(pdfBytes),
            });

        var sut = CreateSut(handlerMock.Object);

        var result = await sut.DownloadReceiptPdfAsync(guid, ct);

        result.Should().Equal(pdfBytes);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("123")]
    [InlineData("../etc/passwd")]
    [InlineData("id?inject=true")]
    [InlineData("")]
    public async Task DownloadReceiptPdfAsync_WithNonGuidId_ThrowsArgumentException(string invalidId)
    {
        var sut = CreateSut(new Mock<HttpMessageHandler>().Object);

        var act = async () => await sut.DownloadReceiptPdfAsync(invalidId);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("transactionId");
    }

    // -------------------------------------------------------------------------
    // H3: PDF download size limit
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DownloadReceiptPdfAsync_WhenContentLengthExceedsLimit_ThrowsBeforeDownload()
    {
        var guid = Guid.NewGuid().ToString();

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(new byte[1]),
                };
                response.Content.Headers.ContentLength = SumUpApiClient.MaxReceiptPdfSizeBytes + 1;
                return response;
            });

        var sut = CreateSut(handlerMock.Object);

        var act = async () => await sut.DownloadReceiptPdfAsync(guid);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*exceeds maximum allowed size*");
    }

    [Fact]
    public async Task DownloadReceiptPdfAsync_WhenBodyExceedsLimit_ThrowsAfterDownload()
    {
        // Simulate server that lies about Content-Length (no header) but sends oversized body
        var guid = Guid.NewGuid().ToString();
        var oversizedBody = new byte[SumUpApiClient.MaxReceiptPdfSizeBytes + 1];

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(oversizedBody),
            });

        var sut = CreateSut(handlerMock.Object);

        var act = async () => await sut.DownloadReceiptPdfAsync(guid);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*exceeds maximum allowed size*");
    }

    [Fact]
    public async Task DownloadReceiptPdfAsync_WhenBodyIsWithinLimit_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var guid = Guid.NewGuid().ToString();
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 };

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(pdfBytes),
            });

        var sut = CreateSut(handlerMock.Object);

        var result = await sut.DownloadReceiptPdfAsync(guid, ct);

        result.Should().Equal(pdfBytes);
    }
}
