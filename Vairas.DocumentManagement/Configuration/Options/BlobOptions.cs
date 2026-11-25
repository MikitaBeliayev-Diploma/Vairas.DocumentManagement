namespace Vairas.DocumentManagement.Configuration.Options;

public sealed class BlobOptions
{
    public string? ConnectionString { get; set; }
    public string? ServiceUri { get; set; }
    public string Container { get; set; } = "documents";
}
