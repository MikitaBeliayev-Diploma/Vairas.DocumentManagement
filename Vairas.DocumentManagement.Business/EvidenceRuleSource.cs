using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

/// <summary>Exact rule edition retained by the published Learning programme version.</summary>
public sealed record EvidenceRuleSource(string Code, string SourceUri, string Edition, string Applicability, Guid? VerifiedBy, DateTimeOffset? VerifiedAt);
