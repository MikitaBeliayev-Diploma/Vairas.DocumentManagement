using System.Linq.Expressions;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

public interface IDeliveryCatalogue
{
    Task<(Guid School, Guid Id)[]> JobsAsync(CancellationToken ct);
    Task<(Guid School, Guid Id)[]> PublicationsAsync(CancellationToken ct);
    Task<(Guid School, Guid Enrollment)[]> EnrollmentsAsync(CancellationToken ct);
    Task<(Guid School, Guid Id)[]> FilesAsync(CancellationToken ct);
}
