using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

/// <summary>Create a school-owned typed template.</summary>
public sealed record CreateTemplateRequest(string Kind, string Name);
