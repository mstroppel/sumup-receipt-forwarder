using Microsoft.Extensions.Options;

namespace Rafatz.SumUpReceiptForwarder;

public class SumUpReceiptForwarderWorker(
    ILogger<SumUpReceiptForwarderWorker> _logger,
    IOptions<SumUpReceiptForwarderSettings> _options) : BackgroundService
{
    private readonly SumUpReceiptForwarderSettings _settings = _options.Value;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SumUpReceiptForwarder worker started");

        while (!stoppingToken.IsCancellationRequested)
        {

            _logger.LogInformation("Waiting for {Minutes} minutes until next sync cycle", _settings.WorkerDelay);
            await Task.Delay(TimeSpan.FromMinutes(_settings.WorkerDelay), stoppingToken);
        }

        _logger.LogInformation("SumUpReceiptForwarder worker stopped");
    }
}
