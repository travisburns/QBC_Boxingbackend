namespace QBC.Api.Domain;

/// <summary>
/// Idempotency log for incoming Square webhooks. We record each event id so a
/// re-delivered webhook is processed at most once.
/// </summary>
public class WebhookEvent
{
    public int Id { get; set; }
    public string EventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
}
