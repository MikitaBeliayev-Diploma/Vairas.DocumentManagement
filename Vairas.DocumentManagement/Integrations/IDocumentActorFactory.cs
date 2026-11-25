using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Vairas.DocumentManagement.Business;
using Vairas.DocumentManagement.Middleware;

namespace Vairas.DocumentManagement.Integrations;


public interface IDocumentActorFactory { Task<DocumentActor> CreateAsync(HttpContext http, CancellationToken ct); }
