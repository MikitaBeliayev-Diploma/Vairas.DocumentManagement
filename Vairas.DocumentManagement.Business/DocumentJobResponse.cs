using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

/// <summary>Generation progress independent of document lifecycle and readiness.</summary>
public sealed record DocumentJobResponse(Guid JobId, string Status, Guid? DocumentId, bool Ready, string[] MissingItems, string SourceSnapshotVersion, string? DocumentState, string? ErrorCode);
