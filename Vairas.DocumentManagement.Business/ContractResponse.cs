using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

/// <summary>Retained immutable contract and current copy metadata.</summary>
public sealed record ContractResponse(Guid ContractId, Guid EnrollmentId, Guid StudentId, Guid TemplateVersionId, string Status, long Revision, Guid? ReplacesContractId, SignedCopyResponse[] SignedCopies);
