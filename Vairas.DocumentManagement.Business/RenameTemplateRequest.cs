using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

/// <summary>Rename while comparing the current root revision.</summary>
public sealed record RenameTemplateRequest(string Name, long ExpectedRevision);
