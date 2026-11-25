using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

/// <summary>Authenticated human identity, never supplied in public request JSON.</summary>
public sealed record DocumentActor(Guid IdentityId, Guid SessionId, Guid UserId, Guid? StudentId, Guid? TeacherId, DateTimeOffset ValidUntil, string CorrelationId);
