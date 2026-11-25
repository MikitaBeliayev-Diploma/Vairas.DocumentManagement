using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

/// <summary>Publish as Ready or explicitly Review-only after recorded review.</summary>
public sealed record PublishRequest(long ExpectedRevision, long ExpectedVersionRevision, string Mode, string Reason);
