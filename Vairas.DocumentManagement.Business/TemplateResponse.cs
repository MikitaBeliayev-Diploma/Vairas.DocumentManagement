using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

/// <summary>Template catalogue item with immutable child metadata.</summary>
public sealed record TemplateResponse(Guid TemplateId, Guid SchoolId, string Kind, string Name, Guid? ActiveVersionId, long Revision, TemplateVersionResponse[] Versions);
