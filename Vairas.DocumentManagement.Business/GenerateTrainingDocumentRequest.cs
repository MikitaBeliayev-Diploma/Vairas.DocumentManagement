using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

/// <summary>Asynchronous document generation from an exact owner snapshot; template is explicit when the catalogue is ambiguous.</summary>
public sealed record GenerateTrainingDocumentRequest(string SourceSnapshotVersion, Guid? TemplateId = null);
