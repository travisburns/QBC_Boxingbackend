using QBC.Api.Domain;

namespace QBC.Api.Services.Square;

/// <summary>
/// Server-to-server gateway to Square. Everything here uses the gym owner's
/// access token, which lives only on the server. The browser only ever hands us
/// a single-use card token (source id) — never raw card data.
/// </summary>
public interface ISquareGateway
{
    Task<string> EnsureCustomerAsync(ApplicationUser user, string? existingCustomerId, CancellationToken ct);

    Task<SquareCardResult> CreateCardOnFileAsync(
        string customerId, string sourceId, string idempotencyKey, CancellationToken ct);

    Task<SquareSubscriptionResult> CreateSubscriptionAsync(
        string customerId, string planVariationId, string cardId, string idempotencyKey, CancellationToken ct);

    Task<SquareSubscriptionResult> UpdateSubscriptionCardAsync(
        string subscriptionId, string cardId, CancellationToken ct);

    Task<SquareSubscriptionResult> CancelSubscriptionAsync(string subscriptionId, CancellationToken ct);

    Task<SquareSubscriptionResult?> GetSubscriptionAsync(string subscriptionId, CancellationToken ct);

    /// <summary>Verifies the HMAC-SHA256 signature on an incoming webhook.</summary>
    bool VerifyWebhookSignature(string signatureHeader, string requestBody);
}
