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

        if (!_settings.SmtpUseTls)
        {
            logger.LogError(
                "SMTP TLS is disabled. Refusing to send email to protect credentials and content from plaintext transmission to {Host}:{Port}",
                _settings.SmtpHost, _settings.SmtpPort);
            throw new InvalidOperationException(
                $"SMTP TLS is required but disabled for {_settings.SmtpHost}:{_settings.SmtpPort}. " +
                "Set SMTP_USE_TLS=true to enable secure transmission.");
        }

        logger.LogDebug("Connecting to SMTP server {Host}:{Port} (TLS: {UseTls})",
            _settings.SmtpHost, _settings.SmtpPort, _settings.SmtpUseTls);

        await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, SecureSocketOptions.StartTls, cancellationToken);
        await client.AuthenticateAsync(_settings.SmtpUsername, _settings.SmtpPassword, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);

        logger.LogInformation("Sent receipt email to {Recipient} with attachment {FileName}",
            recipientEmail, pdfFileName);
    }
}
