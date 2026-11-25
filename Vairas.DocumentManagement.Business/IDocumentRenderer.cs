using System.Linq.Expressions;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

public interface IDocumentRenderer
{
    void ValidateTemplate(byte[] docx, string kind);
    Task<RenderOutput> RenderAsync(byte[] template, RenderBasis basis, CancellationToken ct);
}
