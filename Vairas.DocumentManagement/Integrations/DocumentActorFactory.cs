using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Vairas.DocumentManagement.Business;
using Vairas.DocumentManagement.Middleware;
using Microsoft.Extensions.Options;
using Vairas.DocumentManagement.Configuration.Options;

namespace Vairas.DocumentManagement.Integrations;


public sealed class DocumentActorFactory(IDependencyTransport transport, IOptions<DependencyOptions> optionsSettings, TimeProvider clock) : IDocumentActorFactory
{
    private readonly DependencyOptions options = optionsSettings.Value;
    public async Task<DocumentActor> CreateAsync(HttpContext http, CancellationToken ct)
    {
        var user = await http.AuthenticateAsync("User");
        if (!user.Succeeded || user.Principal is null || !Guid.TryParse(user.Principal.FindFirstValue("sub"),out var identity) || !Guid.TryParse(user.Principal.FindFirstValue("sid"),out var sid)) throw new UnauthorizedException("INVALID_IDENTITY");
        var start = clock.GetUtcNow();
        var session = await transport.SendAsync<SessionResponse>(options.AuthBaseUrl,"internal/v1/sessions/validate",new { identityId = identity, sessionId = sid },null,ct);
        if (!session.Active || session.IdentityId != identity || session.SessionId != sid || session.ValidUntil <= clock.GetUtcNow()) throw new UnauthorizedException("SESSION_INVALID");
        var header = http.Request.Headers.Authorization.ToString(); if (!header.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedException("AUTHENTICATION_REQUIRED");
        var profile = await transport.SendAsync<ActorProfile>(options.UserManagementBaseUrl,"api/v1/users/me",null,header[7..],ct);
        if (profile.UserId == Guid.Empty || profile.Status != "Active") throw DocumentErrors.Denied();
        return new(identity,sid,profile.UserId,profile.StudentId,profile.TeacherId,session.ValidUntil < start.AddSeconds(30) ? session.ValidUntil : start.AddSeconds(30),CorrelationMiddleware.Id(http));
    }
    internal sealed record SessionResponse(bool Active, Guid IdentityId, Guid SessionId, DateTimeOffset ValidUntil);
    private sealed record ActorProfile(Guid UserId, string Status, Guid? StudentId, Guid? TeacherId);
}
