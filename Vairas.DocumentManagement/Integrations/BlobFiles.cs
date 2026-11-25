using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Vairas.DocumentManagement.Business;
using Vairas.DocumentManagement.Configuration.Options;

namespace Vairas.DocumentManagement.Integrations;


public sealed class BlobFiles(BlobContainerClient container) : IPrivateFiles
{
    public static BlobContainerClient Create(BlobOptions options) => (!string.IsNullOrWhiteSpace(options.ConnectionString)
        ? new BlobServiceClient(options.ConnectionString) : new BlobServiceClient(new Uri(options.ServiceUri ?? throw DocumentErrors.Dependency("BLOB_NOT_CONFIGURED")),new DefaultAzureCredential())).GetBlobContainerClient(options.Container);
    public async Task PutAsync(string key, byte[] bytes, string mediaType, CancellationToken ct)
    {
        using var stream = new MemoryStream(bytes,false);
        await container.GetBlobClient(key).UploadAsync(stream,new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = mediaType }, Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All } },ct);
    }
    public async Task<byte[]> GetAsync(string key, long expectedSize, CancellationToken ct)
    {
        if (expectedSize is <= 0 or > 200 * 1024 * 1024) throw DocumentErrors.Dependency("INVALID_FILE_SIZE");
        var result = await container.GetBlobClient(key).DownloadStreamingAsync(cancellationToken:ct);
        await using var source = result.Value.Content;
        using var destination = new MemoryStream(); var buffer = new byte[65536]; int count;
        while ((count = await source.ReadAsync(buffer,ct)) > 0)
        { if (destination.Length+count > expectedSize) throw DocumentErrors.Dependency("FILE_INTEGRITY_FAILURE"); await destination.WriteAsync(buffer.AsMemory(0,count),ct); }
        return destination.ToArray();
    }
}
