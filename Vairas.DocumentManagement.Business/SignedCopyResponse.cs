using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

/// <summary>Externally signed copy registration; no cryptographic signature assurance.</summary>
public sealed record SignedCopyResponse(Guid SignedCopyId, int CopyNo, Guid FileId, bool IsCurrent, DateTimeOffset DeclaredSignedAt, DateTimeOffset RecordedAt, string Basis);
