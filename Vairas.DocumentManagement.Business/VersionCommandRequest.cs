using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

/// <summary>Review/publication command compares parent and child independently.</summary>
public sealed record VersionCommandRequest(long ExpectedRevision, long ExpectedVersionRevision, string Reason);
