using System.Linq.Expressions;
using System.Text.Json;
using Vairas.DocumentManagement.Business;
using Vairas.DocumentManagement.Domain;
using Xunit;

namespace Vairas.DocumentManagement.Tests;

public sealed class MemoryStore : IDocumentStore
{
    private Dictionary<Type,List<ISchoolRow>> rows = new(); private readonly SemaphoreSlim gate = new(1);
    public async Task<IDocumentSession> OpenAsync(Guid school, bool write, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        var copy = rows.ToDictionary(x => x.Key,x => x.Value.Select(v => (ISchoolRow)JsonSerializer.Deserialize(JsonSerializer.Serialize(v,v.GetType()),v.GetType())!).ToList());
        if (!write) gate.Release(); return new Session(this,copy,school,write);
    }
    private sealed class Session(MemoryStore owner, Dictionary<Type,List<ISchoolRow>> rows, Guid school, bool write) : IDocumentSession
    {
        private List<ISchoolRow> Table<T>() => rows.TryGetValue(typeof(T),out var list) ? list : rows[typeof(T)] = [];
        public Task<T?> FindAsync<T>(Expression<Func<T,bool>> predicate, CancellationToken ct) where T : class, ISchoolRow => Task.FromResult(Table<T>().Cast<T>().Where(x => x.SchoolId == school).SingleOrDefault(predicate.Compile()));
        public Task<IReadOnlyList<T>> ListAsync<T>(Expression<Func<T,bool>> predicate, CancellationToken ct) where T : class, ISchoolRow => Task.FromResult<IReadOnlyList<T>>(Table<T>().Cast<T>().Where(x => x.SchoolId == school).Where(predicate.Compile()).ToArray());
        public void Add<T>(T row) where T : class, ISchoolRow { Assert.Equal(school,row.SchoolId); Table<T>().Add(row); }
        public Task FlushAsync(CancellationToken ct) => Task.CompletedTask;
        public Task CommitAsync(CancellationToken ct) { if (write) owner.rows = rows; return Task.CompletedTask; }
        public ValueTask DisposeAsync() { if (write) owner.gate.Release(); return ValueTask.CompletedTask; }
    }
}
