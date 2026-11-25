using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

/// <summary>One immutable template file and its lifecycle.</summary>
public sealed record TemplateVersionResponse(Guid TemplateVersionId, int VersionNo, string Status, Guid FileId);
