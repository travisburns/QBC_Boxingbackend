using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using QBC.Api.Models;
using QBC.Api.Options;

namespace QBC.Api.Services.Square;

public sealed class SquareGateway(
    HttpClient http,
    IOptions<SquareOptions> options,
    ILogger<SquareGateway> logger) : ISquareGateway
{
    private readonly SquareOptions _opt = options.Value;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<string> EnsureCustomerAsync(
        ApplicationUser user, string? existingCustomerId, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(existingCustomerId))
            return existingCustomerId;

        var body = new
        {
            idempotency_key = Guid.NewGuid().ToString(),
            given_name = user.FirstName,
            family_name = user.LastName,
            email_address = user.Email,
            reference_id = user.Id,
        };

        using var doc = await PostAsync("/v2/customers", body, ct);
        return doc.RootElement.GetProperty("customer").GetProperty("id").GetString()
               ?? throw new SquareApiException("Square did not return a customer id.");
    }

    public async Task<SquareCardResult> CreateCardOnFileAsync(
        string customerId, string sourceId, string idempotencyKey, CancellationToken ct)
    {
        var body = new
        {
            idempotency_key = idempotencyKey,
            source_id = sourceId,
            card = new { customer_id = customerId },
        };

        using var doc = await PostAsync("/v2/cards", body, ct);
        var card = doc.RootElement.GetProperty("card");
        return new SquareCardResult(
            card.GetProperty("id").GetString()!,
            card.TryGetProperty("card_brand", out var b) ? b.GetString() : null,
            card.TryGetProperty("last_4", out var l) ? l.GetString() : null);
    }

    public async Task<SquareSubscriptionResult> CreateSubscriptionAsync(
        string customerId, string planVariationId, string cardId, string idempotencyKey, CancellationToken ct)
    {
        var body = new
        {
            idempotency_key = idempotencyKey,
            location_id = _opt.LocationId,
            plan_variation_id = planVariationId,
            customer_id = customerId,
            card_id = cardId,
        };

        using var doc = await PostAsync("/v2/subscriptions", body, ct);
        return ParseSubscription(doc.RootElement.GetProperty("subscription"));
    }

    public async Task<SquareSubscriptionResult> UpdateSubscriptionCardAsync(
        string subscriptionId, string cardId, CancellationToken ct)
    {
        var body = new { subscription = new { card_id = cardId } };
        using var res = await http.PutAsJsonAsync($"/v2/subscriptions/{subscriptionId}", body, JsonOpts, ct);
        var content = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            logger.LogWarning("Square update-subscription error: {Status} {Body}", (int)res.StatusCode, content);
            throw new SquareApiException(ExtractError(content));
        }
        using var doc = JsonDocument.Parse(content);
        return ParseSubscription(doc.RootElement.GetProperty("subscription"));
    }

    public async Task<SquareSubscriptionResult> CancelSubscriptionAsync(
        string subscriptionId, CancellationToken ct)
    {
        // Square schedules cancellation for the end of the current billing period.
        using var doc = await PostAsync($"/v2/subscriptions/{subscriptionId}/cancel", new { }, ct);
        return ParseSubscription(doc.RootElement.GetProperty("subscription"));
    }

    public async Task<SquareSubscriptionResult?> GetSubscriptionAsync(
        string subscriptionId, CancellationToken ct)
    {
        using var res = await http.GetAsync($"/v2/subscriptions/{subscriptionId}", ct);
        if (!res.IsSuccessStatusCode) return null;
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        return doc.RootElement.TryGetProperty("subscription", out var sub)
            ? ParseSubscription(sub)
            : null;
    }

    public bool VerifyWebhookSignature(string signatureHeader, string requestBody)
    {
        if (string.IsNullOrEmpty(_opt.WebhookSignatureKey) ||
            string.IsNullOrEmpty(_opt.WebhookNotificationUrl) ||
            string.IsNullOrEmpty(signatureHeader))
        {
            return false;
        }

        // Square signs: HMAC-SHA256(notificationUrl + rawBody) with the signature key.
        var payload = _opt.WebhookNotificationUrl + requestBody;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_opt.WebhookSignatureKey));
        var computed = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));

        byte[] provided;
        try { provided = Convert.FromBase64String(signatureHeader); }
        catch (FormatException) { return false; }

        return CryptographicOperations.FixedTimeEquals(computed, provided);
    }

    // ---- helpers ----

    private static SquareSubscriptionResult ParseSubscription(JsonElement sub)
    {
        var id = sub.GetProperty("id").GetString()!;
        var status = sub.TryGetProperty("status", out var s) ? s.GetString() ?? "PENDING" : "PENDING";
        var cardId = sub.TryGetProperty("card_id", out var c) ? c.GetString() : null;

        DateTime? chargedThrough = null;
        if (sub.TryGetProperty("charged_through_date", out var ctd) &&
            DateTime.TryParse(ctd.GetString(), out var parsed))
        {
            chargedThrough = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }

        return new SquareSubscriptionResult(id, status, cardId, chargedThrough);
    }

    private async Task<JsonDocument> PostAsync(string path, object body, CancellationToken ct)
    {
        using var res = await http.PostAsJsonAsync(path, body, JsonOpts, ct);
        var content = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
        {
            var message = ExtractError(content);
            // Log full detail server-side; surface a safe message to callers.
            logger.LogWarning("Square API error on {Path}: {Status} {Body}", path, (int)res.StatusCode, content);
            throw new SquareApiException(message);
        }

        return JsonDocument.Parse(content);
    }

    private static string ExtractError(string content)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0)
            {
                var first = errors[0];
                var detail = first.TryGetProperty("detail", out var d) ? d.GetString() : null;
                if (!string.IsNullOrWhiteSpace(detail)) return detail!;
            }
        }
        catch (JsonException) { /* fall through */ }
        return "The payment provider rejected the request.";
    }
}
