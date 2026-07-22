namespace QBC.Api.Options;

/// <summary>
/// Admin (owner CRM) access. Any registered account whose email is listed here
/// is granted the "Admin" role at startup, unlocking the customer CRM endpoints.
/// Set via config, e.g. Admin:Emails:0 = "owner@qbcboxing.com".
/// </summary>
public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    /// <summary>The role name that gates the CRM.</summary>
    public const string RoleName = "Admin";

    /// <summary>Emails that should hold the Admin role (the gym owner / staff).</summary>
    public string[] Emails { get; set; } = [];
}
