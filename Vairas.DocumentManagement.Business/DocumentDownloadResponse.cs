using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

/// <summary>Opaque authenticated service URL; private Blob URLs are never exposed.</summary>
public sealed record DocumentDownloadResponse(Guid DocumentId, string DownloadUrl, DateTimeOffset ExpiresAt);
