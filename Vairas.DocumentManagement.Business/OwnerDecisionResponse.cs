using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

/// <summary>Retained owner decision at a known revision; historical acceptance is not a freshness lease.</summary>
public sealed record OwnerDecisionResponse(Guid OperationId, string Status, long EvaluatedRevision, DateTimeOffset DecidedAt, string Reason);
