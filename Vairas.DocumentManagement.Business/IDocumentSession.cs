using System.Linq.Expressions;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

public interface IDocumentSession : IAsyncDisposable
{
    Task<T?> FindAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken ct) where T : class, ISchoolRow;
    Task<IReadOnlyList<T>> ListAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken ct) where T : class, ISchoolRow;
    void Add<T>(T row) where T : class, ISchoolRow;
    Task FlushAsync(CancellationToken ct);
    Task CommitAsync(CancellationToken ct);
}
