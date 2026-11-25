using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

/// <summary>Owner evidence with immutable facts and separately evaluated freshness.</summary>
public sealed record EvidencePayload(Guid SchoolId, Guid EnrollmentId, Guid SnapshotId, string Kind, long DependencyRevision, long CurrentDependencyRevision, string Status,
    bool IsCurrent, bool SourcesVerified, string SourceSnapshotVersion, string SchemaVersion, DateTimeOffset PreparedAt, EvidenceRow[] Rows, EvidenceCheck[] Checks,
    Guid? ProgramVersionId = null, Guid? ChecklistVersionId = null, EvidenceRuleSource[]? RuleSources = null);
