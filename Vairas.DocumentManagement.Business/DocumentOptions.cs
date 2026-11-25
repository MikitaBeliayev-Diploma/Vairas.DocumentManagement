using System.Linq.Expressions;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

public sealed class DocumentOptions
{
    public long MaxUploadBytes { get; set; } = 20 * 1024 * 1024;
    public long MaxExpandedDocxBytes { get; set; } = 100 * 1024 * 1024;
    public int RendererTimeoutSeconds { get; set; } = 60;
    public string PublicOrigin { get; set; } = "https://localhost:7700";
    public bool WorkersEnabled { get; set; }
}
