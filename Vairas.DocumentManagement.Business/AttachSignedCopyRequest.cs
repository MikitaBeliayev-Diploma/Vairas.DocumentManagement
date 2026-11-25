using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

/// <summary>Attach an already scanned PDF and declare its external signing basis.</summary>
public sealed record AttachSignedCopyRequest(Guid FileId, long ExpectedRevision, DateTimeOffset DeclaredSignedAt, string Basis);
