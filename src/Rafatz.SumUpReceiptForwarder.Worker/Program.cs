using Microsoft.Extensions.Options;
using Rafatz.SumUpReceiptForwarder;
using Rafatz.SumUpReceiptForwarder.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

builder.Services.AddSingleton<IOptions<SumUpReceiptForwarderSettings>>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var settings = new SumUpReceiptForwarderSettings
    {
        SyncIntervalMinutes = config.GetValueOrThrow<int>("SYNC_INTERVAL_MINUTES"),
    };
    return Options.Create(settings);
});

builder.Services.AddHostedService<SumUpReceiptForwarderWorker>();

var host = builder.Build();
host.Run();
