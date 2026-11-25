using System.Linq.Expressions;
using System.Text.Json;
using Vairas.DocumentManagement.Business;
using Vairas.DocumentManagement.Domain;
using Xunit;

namespace Vairas.DocumentManagement.Tests;

public sealed class TestOwners(TestClock clock) : IDocumentOwners
{
    public Guid Student { get; } = Guid.NewGuid();
    public long Revision { get; set; } = 1;
    public long SchoolRevision { get; set; } = 1;
    public bool Admin { get; set; } = true;
    public bool Self { get; set; }
    public bool Teacher { get; set; }
    public bool Available { get; set; } = true;
    public bool Active { get; set; } = true;
    public bool Ready { get; set; } = true;
    public string EnrollmentStatus { get; set; } = "Active";
    public bool LoseDecisionReply { get; set; }
    public EvidencePayload Evidence { get; set; } = null!;
    public Dictionary<Guid,(string Digest,OwnerDecisionResponse Result)> Decisions { get; } = new();
    public Task<DocumentActor> ReadmitAsync(DocumentActor actor, CancellationToken ct) => !Active ? throw DocumentErrors.Denied() : !Available ? throw DocumentErrors.Dependency() : Task.FromResult(actor with { ValidUntil = clock.Time.AddSeconds(30) });
    public Task<DocumentAuthority> AuthorityAsync(Guid school, Guid? enrollment, DocumentActor actor, CancellationToken ct) => !Available ? throw DocumentErrors.Dependency() : !Active ? throw DocumentErrors.Denied() :
        Task.FromResult(new DocumentAuthority(school,actor.UserId,enrollment,enrollment is null ? null : Student,"Synthetic School","B",EnrollmentStatus,SchoolRevision,1,Admin,Self,Teacher,clock.Time.AddSeconds(30)));
    public Task<DocumentProfile> ProfileAsync(Guid studentId, CancellationToken ct) => Task.FromResult(new DocumentProfile(Student,"Synthetic","Learner",1));
    public Task<EvidencePayload> EvidenceAsync(Guid school, Guid enrollment, string version, DocumentActor actor, CancellationToken ct) => Task.FromResult(Evidence);
    public Task<OwnerDecisionResponse> DecideAsync(Guid operation, OwnerDecisionRequest request, CancellationToken ct)
    {
        if (!Available) throw DocumentErrors.Dependency();
        var digest = Wire.Save(request);
        if (!Decisions.TryGetValue(operation,out var prior)) { prior = (digest,new(operation,Ready && request.ExpectedDependencyRevision == Revision ? "Accepted" : "Rejected",Revision,clock.Time,"Owner decision fixture.")); Decisions.Add(operation,prior); }
        else if (prior.Digest != digest) throw DocumentErrors.Conflict();
        if (LoseDecisionReply) { LoseDecisionReply = false; throw DocumentErrors.Dependency(); }
        return Task.FromResult(prior.Result);
    }
    public Task<OwnerDecisionResponse?> DecisionAsync(Guid school, Guid operation, Guid actor, CancellationToken ct) => Task.FromResult<OwnerDecisionResponse?>(Decisions.TryGetValue(operation,out var result) ? result.Result : null);
    public Task<long> RevisionAsync(Guid school, Guid enrollment, CancellationToken ct) => Available ? Task.FromResult(Revision) : throw DocumentErrors.Dependency();
}
