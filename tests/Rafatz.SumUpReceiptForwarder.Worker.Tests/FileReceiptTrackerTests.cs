using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Rafatz.SumUpReceiptForwarder.Services;

namespace Rafatz.SumUpReceiptForwarder.Worker.Tests;

public class FileReceiptTrackerTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _testFilePath;
    private readonly Mock<ILogger<FileReceiptTracker>> _loggerMock = new();

    public FileReceiptTrackerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"receipt-tracker-test-{Guid.NewGuid():N}");
        _testFilePath = Path.Combine(_testDir, "sent-receipts.txt");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    private FileReceiptTracker CreateTracker() =>
        new(_loggerMock.Object, _testDir);

    [Fact]
    public void IsAlreadySent_WhenNeverSent_ReturnsFalse()
    {
        var tracker = CreateTracker();

        tracker.IsAlreadySent("some-id").Should().BeFalse();
    }

    [Fact]
    public async Task MarkAsSentAsync_ThenIsAlreadySent_ReturnsTrue()
    {
        var ct = TestContext.Current.CancellationToken;
        var tracker = CreateTracker();

        await tracker.MarkAsSentAsync("receipt-123", ct);

        tracker.IsAlreadySent("receipt-123").Should().BeTrue();
    }

    [Fact]
    public async Task MarkAsSentAsync_PersistsToFile()
    {
        var ct = TestContext.Current.CancellationToken;
        var tracker = CreateTracker();

        await tracker.MarkAsSentAsync("receipt-abc", ct);

        File.Exists(_testFilePath).Should().BeTrue();
        var lines = await File.ReadAllLinesAsync(_testFilePath, ct);
        lines.Should().Contain("receipt-abc");
    }

    [Fact]
    public async Task Constructor_LoadsPreviouslySentIds()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange - write some IDs to the file before creating the tracker
        Directory.CreateDirectory(_testDir);
        await File.WriteAllLinesAsync(_testFilePath, ["id-1", "id-2", "id-3"], ct);

        // Act
        var tracker = CreateTracker();

        // Assert
        tracker.IsAlreadySent("id-1").Should().BeTrue();
        tracker.IsAlreadySent("id-2").Should().BeTrue();
        tracker.IsAlreadySent("id-3").Should().BeTrue();
        tracker.IsAlreadySent("id-4").Should().BeFalse();
    }

    [Fact]
    public async Task MarkAsSentAsync_DuplicateId_DoesNotWriteTwice()
    {
        var ct = TestContext.Current.CancellationToken;
        var tracker = CreateTracker();

        await tracker.MarkAsSentAsync("receipt-dup", ct);
        await tracker.MarkAsSentAsync("receipt-dup", ct);

        var lines = (await File.ReadAllLinesAsync(_testFilePath, ct))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray();

        lines.Should().HaveCount(1);
        lines.Should().Contain("receipt-dup");
    }

    [Fact]
    public async Task MarkAsSentAsync_MultipleIds_AllPersisted()
    {
        var ct = TestContext.Current.CancellationToken;
        var tracker = CreateTracker();

        await tracker.MarkAsSentAsync("id-a", ct);
        await tracker.MarkAsSentAsync("id-b", ct);
        await tracker.MarkAsSentAsync("id-c", ct);

        var lines = (await File.ReadAllLinesAsync(_testFilePath, ct))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray();

        lines.Should().HaveCount(3);
        lines.Should().Contain("id-a");
        lines.Should().Contain("id-b");
        lines.Should().Contain("id-c");
    }

    [Fact]
    public void Constructor_WithNoFile_StartsEmpty()
    {
        var tracker = CreateTracker();

        tracker.IsAlreadySent("anything").Should().BeFalse();
    }
}
