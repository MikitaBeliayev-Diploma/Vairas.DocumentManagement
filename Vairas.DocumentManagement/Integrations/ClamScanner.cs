using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using Vairas.DocumentManagement.Business;
using Vairas.DocumentManagement.Configuration.Options;

namespace Vairas.DocumentManagement.Integrations;


public sealed class ClamScanner(ScannerOptions options) : IFileScanner
{
    public async Task<string> ScanAsync(byte[] bytes, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
        try
        {
            using var client = new TcpClient(); await client.ConnectAsync(options.Host,options.Port,timeout.Token);
            await using var stream = client.GetStream(); await stream.WriteAsync("zINSTREAM\0"u8.ToArray(),timeout.Token);
            var size = new byte[4];
            for (var offset = 0; offset < bytes.Length; offset += 65536)
            {
                var count = Math.Min(65536,bytes.Length-offset); BinaryPrimitives.WriteInt32BigEndian(size,count);
                await stream.WriteAsync(size,timeout.Token); await stream.WriteAsync(bytes.AsMemory(offset,count),timeout.Token);
            }
            Array.Clear(size); await stream.WriteAsync(size,timeout.Token);
            using var response = new MemoryStream(); var buffer = new byte[1024];
            while (response.Length < 4096)
            { var n = await stream.ReadAsync(buffer,timeout.Token); if (n == 0) break; response.Write(buffer,0,n); if (buffer.AsSpan(0,n).Contains((byte)0)) break; }
            var text = Encoding.UTF8.GetString(response.ToArray()).TrimEnd('\0','\r','\n');
            return text == "stream: OK" ? "Clean" : text.EndsWith(" FOUND",StringComparison.Ordinal) ? "Rejected" : "Pending";
        }
        catch (Exception e) when (e is SocketException or IOException || e is OperationCanceledException && !ct.IsCancellationRequested) { return "Pending"; }
    }
}
