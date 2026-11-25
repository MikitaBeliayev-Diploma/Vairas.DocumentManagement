using System.Linq.Expressions;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

public interface IDocumentStore { Task<IDocumentSession> OpenAsync(Guid school, bool write, CancellationToken ct); }
