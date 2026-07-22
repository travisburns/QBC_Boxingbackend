namespace QBC.Api.Dtos;

/// <summary>One row in the owner's customer list.</summary>
public sealed record CustomerSummaryDto(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    DateTime JoinedUtc,
    string MembershipStatus,
    string? PlanId,
    string? PlanName,
    DateTime? CurrentPeriodEndUtc);

/// <summary>The customer list plus a few at-a-glance totals for the CRM header.</summary>
public sealed record CustomerListDto(
    int TotalCustomers,
    int ActiveMembers,
    IReadOnlyList<CustomerSummaryDto> Customers);

/// <summary>A single membership record in a customer's history.</summary>
public sealed record MembershipRecordDto(
    string PlanId,
    string? PlanName,
    string Status,
    string? CardBrand,
    string? CardLast4,
    DateTime? CurrentPeriodEndUtc,
    bool CancelAtPeriodEnd,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);

/// <summary>Full customer profile for the CRM detail view.</summary>
public sealed record CustomerDetailDto(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    DateTime JoinedUtc,
    string? SquareCustomerId,
    CustomerSummaryDto Summary,
    IReadOnlyList<MembershipRecordDto> History);
