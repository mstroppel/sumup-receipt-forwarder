using AutoFixture;
using AutoFixture.AutoMoq;
using Microsoft.Extensions.Options;
using Moq;

namespace Rafatz.SumUpReceiptForwarder.Worker.Tests;

public class SumUpReceiptForwarderWorkerTests
{
    private readonly IFixture _fixture = new Fixture().Customize(new AutoMoqCustomization());
    private readonly Mock<IOptions<SumUpReceiptForwarderSettings>> _optionsMock;
    private readonly SumUpReceiptForwarderSettings _settings;
    private readonly SumUpReceiptForwarderWorker _sut;

    public SumUpReceiptForwarderWorkerTests()
    {
        _optionsMock = _fixture.Freeze<Mock<IOptions<SumUpReceiptForwarderSettings>>>();

        _settings = _fixture.Build<SumUpReceiptForwarderSettings>()
            .With(x => x.WorkerDelay, 60)
            .Create();

        _optionsMock.Setup(x => x.Value).Returns(_settings);

        _sut = _fixture.Create<SumUpReceiptForwarderWorker>();
    }

}