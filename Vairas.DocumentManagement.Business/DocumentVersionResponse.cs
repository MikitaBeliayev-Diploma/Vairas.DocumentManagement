using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

/// <summary>Exact published or prepared artifact provenance.</summary>
public sealed record DocumentVersionResponse(Guid DocumentVersionId, int VersionNo, string State, long Revision, Guid SnapshotId, long DependencyRevision, string EvidenceStatus, Guid TemplateVersionId, bool Reviewed);
