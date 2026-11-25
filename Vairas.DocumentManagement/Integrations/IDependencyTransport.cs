using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Vairas.DocumentManagement.Business;
using Vairas.DocumentManagement.Middleware;
namespace Vairas.DocumentManagement.Integrations;


public interface IDependencyTransport
{
    Task<T> SendAsync<T>(string origin, string route, object? body, string? userToken, CancellationToken ct);
}
