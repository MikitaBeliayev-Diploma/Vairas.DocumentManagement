using Microsoft.AspNetCore.Http;
using Vairas.DocumentManagement.Business;
using Vairas.DocumentManagement.Integrations;

namespace Vairas.DocumentManagement.Tests;

public sealed class DocumentTestActorFactory(World world) : IDocumentActorFactory
{
    public Task<DocumentActor> CreateAsync(HttpContext http, CancellationToken ct) => Task.FromResult(world.Actor);
}
