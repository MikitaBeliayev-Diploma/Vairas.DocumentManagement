using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Vairas.DocumentManagement.Business;
using Vairas.DocumentManagement.Middleware;
using Microsoft.Extensions.Options;
using Vairas.DocumentManagement.Configuration.Options;

namespace Vairas.DocumentManagement.Integrations;


public sealed class DocumentOwners(IDependencyTransport transport, IOptions<DependencyOptions> optionsSettings, TimeProvider clock) : IDocumentOwners
{
    private readonly DependencyOptions options = optionsSettings.Value;
    private string Learning => string.IsNullOrEmpty(options.LearningBaseUrl) ? "https://localhost:7500/" : options.LearningBaseUrl;
    public async Task<DocumentActor> ReadmitAsync(DocumentActor actor, CancellationToken ct)
    {
        var began = clock.GetUtcNow();
        var session = await transport.SendAsync<DocumentActorFactory.SessionResponse>(options.AuthBaseUrl,"internal/v1/sessions/validate",new { identityId = actor.IdentityId, sessionId = actor.SessionId },null,ct);
        if (!session.Active || session.IdentityId != actor.IdentityId || session.SessionId != actor.SessionId || session.ValidUntil <= began) throw new UnauthorizedException("SESSION_INVALID");
        var status = await transport.SendAsync<StatusResponse>(options.UserManagementBaseUrl,$"internal/v1/users/{actor.UserId:D}/status",null,null,ct);
        if (status.UserId != actor.UserId || !status.Active) throw DocumentErrors.Denied();
        return actor with { ValidUntil = session.ValidUntil < began.AddSeconds(30) ? session.ValidUntil : began.AddSeconds(30) };
    }
    public Task<DocumentAuthority> AuthorityAsync(Guid school, Guid? enrollment, DocumentActor actor, CancellationToken ct) =>
        transport.SendAsync<DocumentAuthority>(options.SchoolBaseUrl,"internal/v1/documents/authority",new { schoolId = school, enrollmentId = enrollment, actorUserId = actor.UserId, teacherId = actor.TeacherId },null,ct);
    public async Task<DocumentProfile> ProfileAsync(Guid studentId, CancellationToken ct)
    {
        var binding = await transport.SendAsync<BindingResponse>(options.UserManagementBaseUrl,$"internal/v1/profiles/student/{studentId:D}",null,null,ct);
        if (binding.ProfileId != studentId || binding.UserId == Guid.Empty || !binding.Active) throw DocumentErrors.Dependency("INVALID_STUDENT_BINDING");
        var result = await transport.SendAsync<DocumentProfile>(options.UserManagementBaseUrl,$"internal/v1/users/{binding.UserId:D}/document-profile",null,null,ct);
        if (result.UserId != binding.UserId || result.Revision < 1 || string.IsNullOrWhiteSpace(result.FirstName) || string.IsNullOrWhiteSpace(result.LastName)) throw DocumentErrors.Dependency("INVALID_PROFILE_EVIDENCE");
        return result;
    }
    public Task<EvidencePayload> EvidenceAsync(Guid school, Guid enrollment, string version, DocumentActor actor, CancellationToken ct)
    {
        // An opaque source version identifies one exact snapshot; never silently substitute the latest snapshot.
        if (version.Length < 47 || !version.StartsWith("evidence-",StringComparison.Ordinal) || !Guid.TryParse(version.AsSpan(9,36),out var snapshot)) throw DocumentErrors.Invalid("INVALID_SOURCE_SNAPSHOT_VERSION");
        return transport.SendAsync<EvidencePayload>(Learning,$"internal/v1/documents/evidence/{snapshot:D}?schoolId={school:D}&enrollmentId={enrollment:D}&actorUserId={actor.UserId:D}",null,null,ct);
    }
    public Task<OwnerDecisionResponse> DecideAsync(Guid operation, OwnerDecisionRequest request, CancellationToken ct) => transport.SendAsync<OwnerDecisionResponse>(Learning,$"internal/v1/documents/decisions/{operation:D}",request,null,ct);
    public async Task<OwnerDecisionResponse?> DecisionAsync(Guid school, Guid operation, Guid actor, CancellationToken ct)
    {
        try { return await transport.SendAsync<OwnerDecisionResponse>(Learning,$"internal/v1/documents/decisions/{operation:D}?schoolId={school:D}&actorUserId={actor:D}",null,null,ct); }
        catch (NotFoundException) { return null; }
    }
    public async Task<long> RevisionAsync(Guid school, Guid enrollment, CancellationToken ct) => (await transport.SendAsync<RevisionResponse>(Learning,$"internal/v1/documents/enrollments/{enrollment:D}/revision?schoolId={school:D}",null,null,ct)).DependencyRevision;
    private sealed record StatusResponse(Guid UserId, bool Active);
    private sealed record BindingResponse(Guid ProfileId, Guid UserId, bool Active);
    private sealed record RevisionResponse(long DependencyRevision);
}
