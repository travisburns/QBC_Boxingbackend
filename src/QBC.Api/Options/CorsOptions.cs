namespace QBC.Api.Options;

/// <summary>Allowed browser origins for the SPA frontend (bound from "Cors").</summary>
public sealed class FrontendCorsOptions
{
    public const string SectionName = "Cors";
    public string[] AllowedOrigins { get; set; } = ["http://localhost:3000"];
}
