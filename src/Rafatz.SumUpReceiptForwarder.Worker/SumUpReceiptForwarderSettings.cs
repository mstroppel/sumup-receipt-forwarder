namespace Rafatz.SumUpReceiptForwarder;

public class SumUpReceiptForwarderSettings
{
    /// <summary>How often the worker runs the sync, in minutes.</summary>
    public required int WorkerDelay { get; init; }

    /// <summary>SumUp merchant account ID.</summary>
    public required string SumUpAccountId { get; init; }

    /// <summary>SumUp API key for authentication.</summary>
    public required string SumUpApiKey { get; init; }

    /// <summary>SMTP server hostname.</summary>
    public required string SmtpHost { get; init; }

    /// <summary>SMTP server port.</summary>
    public required int SmtpPort { get; init; }

    /// <summary>SMTP authentication username.</summary>
    public required string SmtpUsername { get; init; }

    /// <summary>SMTP authentication password.</summary>
    public required string SmtpPassword { get; init; }

    /// <summary>Enable TLS for SMTP connection.</summary>
    public required bool SmtpUseTls { get; init; }

    /// <summary>Email address used as the sender.</summary>
    public required string SenderEmail { get; init; }

    /// <summary>Destination email address for cash payments.</summary>
    public required string RecipientEmailCash { get; init; }

    /// <summary>Destination email address for card payments.</summary>
    public required string RecipientEmailCard { get; init; }
}
