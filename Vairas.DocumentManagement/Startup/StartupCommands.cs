using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;

namespace Vairas.DocumentManagement.Startup;

/// <summary>
/// Operator commands that run instead of the web host. Offline commands need no configuration; the host commands use the
/// built application and run the same startup validation as a normal start first.
/// </summary>
public static class StartupCommands
{
    /// <summary>Runs an offline command (`--write-demo-templates`, `--render-fixtures`, `--migrate`, `--grant-runtime`).</summary>
    public static async Task<bool> TryRunOfflineAsync(string[] args)
    {
        if (args.Contains("--write-demo-templates")) await DemoFiles.WriteDemoTemplatesAsync();
        else if (args.Contains("--render-fixtures")) await DemoFiles.RenderFixturesAsync();
        else if (args.Contains("--migrate") || args.Contains("--grant-runtime")) await DatabaseCommands.RunAsync(args);
        else return false;
        return true;
    }

    /// <summary>Runs a command that needs the configured services (`--provision-storage`, `--seed-demo`).</summary>
    public static async Task<bool> TryRunWithHostAsync(WebApplication app, string[] args)
    {
        var requested = args.Contains("--provision-storage") || args.Contains("--seed-demo");
        if (!requested)
            return false;
        app.Services.GetRequiredService<IStartupValidator>().Validate();
        if (args.Contains("--provision-storage")) await app.Services.GetRequiredService<BlobContainerClient>().CreateIfNotExistsAsync();
        else await DemoSeed.RunAsync(app);
        return true;
    }
}
