using System.Linq.Expressions;
using System.Text.Json;
using Vairas.DocumentManagement.Business;
using Vairas.DocumentManagement.Domain;
using Xunit;

namespace Vairas.DocumentManagement.Tests;

public sealed class TestFiles : IPrivateFiles
{
    public readonly Dictionary<string,byte[]> Data = new();
    public Task PutAsync(string key, byte[] bytes, string mediaType, CancellationToken ct) { Data.Add(key,bytes.ToArray()); return Task.CompletedTask; }
    public Task<byte[]> GetAsync(string key, long expectedSize, CancellationToken ct) => Task.FromResult(Data[key].ToArray());
}
