using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Vairas.DocumentManagement.Integrations;
using Xunit;
using Vairas.DocumentManagement.Configuration.Options;
namespace Vairas.DocumentManagement.Tests;

[Trait("Category","Infrastructure")]
public sealed class InfrastructureTests
{
    [Fact]
    public async Task Real_private_Azurite_and_ClamAV_enforce_no_overwrite_and_reject_Eicar()
    {
        var storageKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64));
        await using var azurite = new ContainerBuilder("mcr.microsoft.com/azure-storage/azurite:3.35.0").WithPortBinding(10000,true).WithEnvironment("AZURITE_ACCOUNTS","synthetic:"+storageKey)
            .WithCommand("azurite-blob","--blobHost","0.0.0.0","--skipApiVersionCheck").WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(10000)).Build();
        await using var clam = new ContainerBuilder("clamav/clamav:stable").WithPortBinding(3310,true).WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(3310)).Build();
        await Task.WhenAll(azurite.StartAsync(),clam.StartAsync());
        var host = azurite.Hostname; var port = azurite.GetMappedPublicPort(10000);
        var connection = $"DefaultEndpointsProtocol=http;AccountName=synthetic;AccountKey={storageKey};BlobEndpoint=http://{host}:{port}/synthetic;";
        var container = BlobFiles.Create(new BlobOptions { ConnectionString = connection, Container = "document-integration" }); await container.CreateIfNotExistsAsync();
        var files = new BlobFiles(container); var bytes = "%PDF-1.7 clean fixture"u8.ToArray();
        await files.PutAsync("school/file",bytes,"application/pdf",default); Assert.Equal(bytes,await files.GetAsync("school/file",bytes.Length,default));
        await Assert.ThrowsAsync<Azure.RequestFailedException>(() => files.PutAsync("school/file",[1],"application/pdf",default));
        var scanner = new ClamScanner(new() { Host = clam.Hostname, Port = clam.GetMappedPublicPort(3310), TimeoutSeconds = 20 });
        Assert.Equal("Clean",await scanner.ScanAsync(bytes,default));
        var eicar = Encoding.ASCII.GetBytes("X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*");
        Assert.Equal("Rejected",await scanner.ScanAsync(eicar,default));
        using var anonymous = new HttpClient(); using var response = await anonymous.GetAsync(container.GetBlobClient("school/file").Uri); Assert.False(response.IsSuccessStatusCode);
    }
}
