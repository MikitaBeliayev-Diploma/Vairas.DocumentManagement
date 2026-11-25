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


public interface IWorkloadTokens
{
    Task<string> GetAsync(CancellationToken ct);
}
