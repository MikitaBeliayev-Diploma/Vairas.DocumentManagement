using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

/// <summary>Document root with immutable version metadata and an explicit selected publication.</summary>
public sealed record DocumentResponse(Guid DocumentId, Guid EnrollmentId, string Kind, long Revision, Guid? CurrentVersionId, DocumentVersionResponse[] Versions);
