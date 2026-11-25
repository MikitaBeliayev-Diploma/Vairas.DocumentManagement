using System.Linq.Expressions;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

public interface IPrivateFiles
{
    Task PutAsync(string key, byte[] bytes, string mediaType, CancellationToken ct);
    Task<byte[]> GetAsync(string key, long expectedSize, CancellationToken ct);
}
