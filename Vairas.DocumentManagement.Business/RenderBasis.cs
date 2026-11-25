using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

/// <summary>Validated rendering inputs frozen as private SourceBasis.</summary>
public sealed record RenderBasis(string SchemaVersion, string Kind, Guid SchoolId, Guid EnrollmentId, Guid StudentId, string SchoolName, string Category,
    string StudentName, long SchoolRevision, long EnrollmentRevision, long ProfileRevision, Guid TemplateVersionId, DateTimeOffset GeneratedAt, EvidencePayload? Evidence, Guid? GenerationId = null);
