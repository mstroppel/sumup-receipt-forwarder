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
        WorkerDelay = config.GetValueOrThrow<int>("WORKER_DELAY"),
    };
    return Options.Create(settings);
});

builder.Services.AddHostedService<SumUpReceiptForwarderWorker>();

var host = builder.Build();
host.Run();
