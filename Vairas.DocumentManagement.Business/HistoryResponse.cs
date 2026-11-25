using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

/// <summary>Safe lifecycle history, excluding source basis and private storage keys.</summary>
public sealed record HistoryResponse(Guid EventId, string Action, long SequenceNo, string? FromState, string ToState, Guid ActorId, DateTimeOffset OccurredAt, string Reason);
