namespace Rafatz.SumUpReceiptForwarder.Services;

/// <summary>Service for sending emails with receipt attachments.</summary>
public interface IEmailService
{
    /// <summary>
    /// Sends an email with a PDF receipt attachment.
    /// </summary>
    /// <param name="subject">Email subject line.</param>
    /// <param name="body">Plain-text email body.</param>
    /// <param name="pdfBytes">The PDF receipt content.</param>
    /// <param name="pdfFileName">File name for the PDF attachment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendReceiptAsync(
        string subject,
        string body,
        byte[] pdfBytes,
        string pdfFileName,
        CancellationToken cancellationToken = default);
}
