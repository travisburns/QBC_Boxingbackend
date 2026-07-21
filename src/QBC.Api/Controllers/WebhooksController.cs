using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using QBC.Api.Data;
using QBC.Api.Models;
using QBC.Api.Services;
using QBC.Api.Services.Square;

namespace QBC.Api.Controllers;

/// <summary>
/// Receives Square webhooks. Every request is signature-verified before we act,
/// and each event id is processed at most once (idempotency log).
/// </summary>
[ApiController]
[Route("api/webhooks/square")]
public sealed class WebhooksController(
    ISquareGateway square,
    IMembershipService memberships,
    AppDbContext db,
    ILogger<WebhooksController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        // Read the raw body exactly as sent — the signature is computed over it.
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync(ct);
        Request.Body.Position = 0;

        var signature = Request.Headers["x-square-hmacsha256-signature"].ToString();
        if (!square.VerifyWebhookSignature(signature, body))
        {
            logger.LogWarning("Rejected Square webhook with invalid signature.");
            return Unauthorized();
        }

        string? eventId = null, eventType = null, subscriptionId = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            eventId = root.TryGetProperty("event_id", out var e) ? e.GetString() : null;
            eventType = root.TryGetProperty("type", out var t) ? t.GetString() : null;
            subscriptionId = ExtractSubscriptionId(root, eventType);
        }
        catch (JsonException)
        {
            return BadRequest();
        }

        // Idempotency: skip events we've already recorded.
        if (!string.IsNullOrEmpty(eventId))
        {
            if (db.WebhookEvents.Any(w => w.EventId == eventId))
                return Ok();

            db.WebhookEvents.Add(new WebhookEvent
            {
                EventId = eventId,
                EventType = eventType ?? "unknown",
            });
            await db.SaveChangesAsync(ct);
        }

        if (!string.IsNullOrEmpty(subscriptionId))
        {
            await memberships.SyncFromSquareAsync(subscriptionId, ct);
        }

        return Ok();
    }

    private static string? ExtractSubscriptionId(JsonElement root, string? eventType)
    {
        if (!root.TryGetProperty("data", out var data)) return null;

        // subscription.* events carry the subscription id directly.
        if (eventType is not null && eventType.StartsWith("subscription", StringComparison.Ordinal))
        {
            return data.TryGetProperty("id", out var id) ? id.GetString() : null;
        }

        // invoice.* events reference the subscription inside the object.
        if (data.TryGetProperty("object", out var obj) &&
            obj.TryGetProperty("invoice", out var invoice) &&
            invoice.TryGetProperty("subscription_id", out var subId))
        {
            return subId.GetString();
        }

        return null;
    }
}
