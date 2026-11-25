using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

/// <summary>Exact expected root revision and auditable reason.</summary>
public sealed record RevisionRequest(long ExpectedRevision, string Reason);
