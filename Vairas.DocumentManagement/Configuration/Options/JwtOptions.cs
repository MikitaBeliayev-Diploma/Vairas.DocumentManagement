namespace Vairas.DocumentManagement.Configuration.Options;

public sealed class JwtOptions
{
    public string Authority { get; set; } = "https://localhost:7100";
    public string Issuer { get; set; } = "https://localhost:7100";
    public string UserAudience { get; set; } = "vairas-document-management";
    public string WorkloadAudience { get; set; } = "vairas-document-management-internal";
    // Local allowlist intersects with signed scopes; absent configuration grants nothing.
    public Dictionary<string, string[]> WorkloadClients { get; set; } = [];
}
