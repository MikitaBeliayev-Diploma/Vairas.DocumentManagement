using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

/// <summary>One enrollment-only immutable evidence observation with version provenance.</summary>
public sealed record EvidenceRow(string Kind, Guid ItemId, Guid? LessonId, string State, int CreditedMinutes, DateTimeOffset? ActualStart, DateTimeOffset? ActualEnd,
    decimal? DistanceKm, string[] Topics, string[] Confirmations, string? Outcome, int? Mark, DateTimeOffset? AssessedAt = null, Guid? FactVersionId = null, Guid? SourceSnapshotId = null);
