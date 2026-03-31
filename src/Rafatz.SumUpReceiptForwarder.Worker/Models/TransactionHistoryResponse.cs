using System.Text.Json.Serialization;

namespace Rafatz.SumUpReceiptForwarder.Models;

/// <summary>Response from the SumUp list transactions history endpoint.</summary>
public class TransactionHistoryResponse
{
    [JsonPropertyName("items")]
    public List<TransactionHistoryItem> Items { get; set; } = [];

    [JsonPropertyName("links")]
    public List<TransactionHistoryLink> Links { get; set; } = [];
}

/// <summary>A single transaction entry from the history listing.</summary>
public class TransactionHistoryItem
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("transaction_code")]
    public string? TransactionCode { get; set; }

    [JsonPropertyName("amount")]
    public decimal? Amount { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime? Timestamp { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("payment_type")]
    public string? PaymentType { get; set; }

    [JsonPropertyName("product_summary")]
    public string? ProductSummary { get; set; }

    [JsonPropertyName("transaction_id")]
    public string? TransactionId { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("card_type")]
    public string? CardType { get; set; }
}

/// <summary>Pagination link for transaction history.</summary>
public class TransactionHistoryLink
{
    [JsonPropertyName("rel")]
    public string? Rel { get; set; }

    [JsonPropertyName("href")]
    public string? Href { get; set; }
}
