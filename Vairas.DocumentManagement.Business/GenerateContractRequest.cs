using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

/// <summary>Asynchronous contract generation using the template's exact active version.</summary>
public sealed record GenerateContractRequest(Guid EnrollmentId, Guid TemplateId, Guid? ReplacesContractId = null);
