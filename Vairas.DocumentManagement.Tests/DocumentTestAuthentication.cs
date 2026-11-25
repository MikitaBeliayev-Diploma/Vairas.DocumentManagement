using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Vairas.DocumentManagement.Tests;

/// <summary>HTTP transport fixture only; JWT cryptography is verified separately by security tests.</summary>
public sealed class DocumentTestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(
        Request.Headers.Authorization == "Bearer synthetic-document-test"
            ? AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "synthetic")], Scheme.Name)), Scheme.Name))
            : AuthenticateResult.NoResult());
}
