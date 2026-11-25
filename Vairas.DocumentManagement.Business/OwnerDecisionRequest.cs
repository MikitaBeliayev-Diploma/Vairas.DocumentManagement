using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

/// <summary>Exact idempotent owner decision request for publication or one current use.</summary>
public sealed record OwnerDecisionRequest(Guid SchoolId, Guid EnrollmentId, Guid SnapshotId, long ExpectedDependencyRevision, Guid DocumentVersionId,
    Guid ActorUserId, string Kind, string ArtifactDigest, string Purpose, Guid? TeacherId = null);
