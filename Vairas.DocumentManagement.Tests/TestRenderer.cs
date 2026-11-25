using System.Linq.Expressions;
using System.Text.Json;
using Vairas.DocumentManagement.Business;
using Vairas.DocumentManagement.Domain;
using Xunit;

namespace Vairas.DocumentManagement.Tests;

public sealed class TestRenderer : IDocumentRenderer
{
    public bool Fail { get; set; }
    public void ValidateTemplate(byte[] docx, string kind) { if (docx.Length == 0) throw DocumentErrors.Invalid(); }
    public Task<RenderOutput> RenderAsync(byte[] template, RenderBasis basis, CancellationToken ct) => Fail ? throw DocumentErrors.Dependency("RENDERER_TIMEOUT") : Task.FromResult(new RenderOutput("DOCX fixture"u8.ToArray(),"%PDF-1.7 fixture"u8.ToArray(),"test-renderer"));
}
