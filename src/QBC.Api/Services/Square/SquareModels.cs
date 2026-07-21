namespace QBC.Api.Services.Square;

/// <summary>Thrown when the Square API returns a non-success response.</summary>
public sealed class SquareApiException(string message) : Exception(message);

public sealed record SquareCardResult(string Id, string? Brand, string? Last4);

public sealed record SquareSubscriptionResult(
    string Id,
    string Status,
    string? CardId,
    DateTime? ChargedThroughUtc);
