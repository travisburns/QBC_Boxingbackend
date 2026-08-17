using QBC.Api.Models;
using QBC.Api.Services.Square;

namespace QBC.Api.Tests.Infrastructure;

/// <summary>
/// In-memory stand-in for <see cref="ISquareGateway"/> so integration tests
/// exercise the real controllers / membership orchestration without any HTTP to
/// Square. Behaviour is fully configurable per test.
/// </summary>
public sealed class FakeSquareGateway : ISquareGateway
{
    public string CustomerId { get; set; } = "cust_test_123";
    public SquareCardResult Card { get; set; } = new("card_test_123", "Visa", "1111");
    public SquareSubscriptionResult Subscription { get; set; } =
        new("sub_test_123", "ACTIVE", "card_test_123", DateTime.UtcNow.AddMonths(1));

    /// <summary>When set, <see cref="CreateSubscriptionAsync"/> throws it (e.g. a decline).</summary>
    public Exception? FailCreateSubscriptionWith { get; set; }

    /// <summary>Controls the result of <see cref="VerifyWebhookSignature"/>.</summary>
    public bool WebhookSignatureValid { get; set; } = true;

    public int EnsureCustomerCalls { get; private set; }
    public int CreateCardCalls { get; private set; }
    public int CreateSubscriptionCalls { get; private set; }
    public int CancelCalls { get; private set; }

    public Task<string> EnsureCustomerAsync(
        ApplicationUser user, string? existingCustomerId, CancellationToken ct)
    {
        EnsureCustomerCalls++;
        return Task.FromResult(string.IsNullOrWhiteSpace(existingCustomerId) ? CustomerId : existingCustomerId);
    }

    public Task<SquareCardResult> CreateCardOnFileAsync(
        string customerId, string sourceId, string idempotencyKey, CancellationToken ct)
    {
        CreateCardCalls++;
        return Task.FromResult(Card);
    }

    public Task<SquareSubscriptionResult> CreateSubscriptionAsync(
        string customerId, string planVariationId, string cardId, string idempotencyKey, CancellationToken ct)
    {
        CreateSubscriptionCalls++;
        if (FailCreateSubscriptionWith is not null) throw FailCreateSubscriptionWith;
        return Task.FromResult(Subscription);
    }

    public Task<SquareSubscriptionResult> UpdateSubscriptionCardAsync(
        string subscriptionId, string cardId, CancellationToken ct)
        => Task.FromResult(Subscription with { CardId = cardId });

    public Task<SquareSubscriptionResult> CancelSubscriptionAsync(
        string subscriptionId, CancellationToken ct)
    {
        CancelCalls++;
        return Task.FromResult(Subscription with { Status = "CANCELED" });
    }

    public Task<SquareSubscriptionResult?> GetSubscriptionAsync(
        string subscriptionId, CancellationToken ct)
        => Task.FromResult<SquareSubscriptionResult?>(Subscription);

    public bool VerifyWebhookSignature(string signatureHeader, string requestBody)
        => WebhookSignatureValid;
}
