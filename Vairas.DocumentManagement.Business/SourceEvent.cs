using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

/// <summary>Source event envelope; bodies are change hints, never a credit ledger.</summary>
public sealed record SourceEvent(Guid EventId, string Producer, string EventType, int EventVersion, Guid SchoolId, Guid EnrollmentId, Guid AggregateId,
    long AggregateVersion, string Payload, string CorrelationId, DateTimeOffset OccurredAt);
