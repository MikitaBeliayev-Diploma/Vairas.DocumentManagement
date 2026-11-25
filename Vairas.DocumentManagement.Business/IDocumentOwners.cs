using System.Linq.Expressions;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

public interface IDocumentOwners
{
    Task<DocumentActor> ReadmitAsync(DocumentActor actor, CancellationToken ct);
    Task<DocumentAuthority> AuthorityAsync(Guid school, Guid? enrollment, DocumentActor actor, CancellationToken ct);
    Task<DocumentProfile> ProfileAsync(Guid studentId, CancellationToken ct);
    Task<EvidencePayload> EvidenceAsync(Guid school, Guid enrollment, string version, DocumentActor actor, CancellationToken ct);
    Task<OwnerDecisionResponse> DecideAsync(Guid operation, OwnerDecisionRequest request, CancellationToken ct);
    Task<OwnerDecisionResponse?> DecisionAsync(Guid school, Guid operation, Guid actor, CancellationToken ct);
    Task<long> RevisionAsync(Guid school, Guid enrollment, CancellationToken ct);
}
