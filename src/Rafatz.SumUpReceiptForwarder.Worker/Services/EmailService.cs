using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Rafatz.SumUpReceiptForwarder.Services;

/// <summary>Email service implementation using MailKit for SMTP delivery.</summary>
public class EmailService(
    IOptions<SumUpReceiptForwarderSettings> options,
    ILogger<EmailService> logger) : IEmailService
{
    private readonly SumUpReceiptForwarderSettings _settings = options.Value;

    /// <inheritdoc />
    public async Task SendReceiptAsync(
        string subject,
        string body,
        string recipientEmail,
        byte[] pdfBytes,
        string pdfFileName,
        CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_settings.SenderEmail));
        message.To.Add(MailboxAddress.Parse(recipientEmail));
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder { TextBody = body };
        bodyBuilder.Attachments.Add(pdfFileName, pdfBytes, ContentType.Parse("application/pdf"));
        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();

        var secureSocketOptions = _settings.SmtpUseTls
            ? SecureSocketOptions.StartTls
            : SecureSocketOptions.None;

        if (!_settings.SmtpUseTls)
        {
            logger.LogWarning(
                "SMTP TLS is disabled. Credentials and email content will be transmitted in plaintext to {Host}:{Port}",
                _settings.SmtpHost, _settings.SmtpPort);
        }

        logger.LogDebug("Connecting to SMTP server {Host}:{Port} (TLS: {UseTls})",
            _settings.SmtpHost, _settings.SmtpPort, _settings.SmtpUseTls);

        await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, secureSocketOptions, cancellationToken);
        await client.AuthenticateAsync(_settings.SmtpUsername, _settings.SmtpPassword, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);

        logger.LogInformation("Sent receipt email to {Recipient} with attachment {FileName}",
            recipientEmail, pdfFileName);
    }
}
