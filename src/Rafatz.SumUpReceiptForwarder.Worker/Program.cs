using Microsoft.Extensions.Options;
using Rafatz.SumUpReceiptForwarder;
using Rafatz.SumUpReceiptForwarder.Extensions;
using Rafatz.SumUpReceiptForwarder.Services;
using SumUp;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

builder.Services.AddSingleton<IOptions<SumUpReceiptForwarderSettings>>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var settings = new SumUpReceiptForwarderSettings
    {
        WorkerDelay = config.GetValueOrThrow<int>("WORKER_DELAY"),
        SumUpAccountId = config.GetValueOrThrow<string>("SUMUP_ACCOUNT_ID"),
        SumUpApiKey = config.GetValueOrThrow<string>("SUMUP_API_KEY"),
        SmtpHost = config.GetValueOrThrow<string>("SMTP_HOST"),
        SmtpPort = config.GetValueOrThrow<int>("SMTP_PORT"),
        SmtpUsername = config.GetValueOrThrow<string>("SMTP_USERNAME"),
        SmtpPassword = config.GetValueOrThrow<string>("SMTP_PASSWORD"),
        SmtpUseTls = config.GetValueOrThrow<bool>("SMTP_USE_TLS"),
        SenderEmail = config.GetValueOrThrow<string>("SENDER_EMAIL"),
        RecipientEmail = config.GetValueOrThrow<string>("RECIPIENT_EMAIL"),
    };
    return Options.Create(settings);
});

builder.Services.AddSingleton(_ =>
{
    var config = builder.Configuration;
    var apiKey = config.GetValueOrThrow<string>("SUMUP_API_KEY");
    return new SumUpClient(new SumUpClientOptions { AccessToken = apiKey });
});

builder.Services.AddHttpClient(SumUpApiClient.ReceiptHttpClientName, client =>
{
    client.BaseAddress = new Uri("https://sales-receipt.sumup.com/");
});

builder.Services.AddSingleton<ISumUpApiClient, SumUpApiClient>();
builder.Services.AddSingleton<IEmailService, EmailService>();
builder.Services.AddSingleton<IReceiptTracker, FileReceiptTracker>();

builder.Services.AddHostedService<SumUpReceiptForwarderWorker>();

var host = builder.Build();
host.Run();
