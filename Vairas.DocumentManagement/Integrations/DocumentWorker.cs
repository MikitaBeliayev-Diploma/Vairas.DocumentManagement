using Vairas.DocumentManagement.Business;
using Microsoft.Extensions.Options;
namespace Vairas.DocumentManagement.Integrations;

public sealed class DocumentWorker(IServiceScopeFactory scopes, IOptions<DocumentOptions> optionsSettings, ILogger<DocumentWorker> logger) : BackgroundService
{
    private readonly DocumentOptions options = optionsSettings.Value;
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!options.WorkersEnabled) return;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope(); var catalogue = scope.ServiceProvider.GetRequiredService<IDeliveryCatalogue>(); var service = scope.ServiceProvider.GetRequiredService<DocumentService>();
                async Task Run(Guid school, Guid id, Func<Guid,Guid,CancellationToken,Task> task)
                { try { await task(school,id,ct); } catch (Exception e) when (!ct.IsCancellationRequested) { logger.LogWarning("Document recovery deferred for {SchoolId}/{OperationId}: {ExceptionType}",school,id,e.GetType().Name); } }
                foreach (var item in await catalogue.FilesAsync(ct)) await Run(item.School,item.Id,service.ScanFileAsync);
                foreach (var item in await catalogue.JobsAsync(ct)) await Run(item.School,item.Id,service.ProcessJobAsync);
                foreach (var item in await catalogue.PublicationsAsync(ct)) await Run(item.School,item.Id,service.ProcessPublicationAsync);
                foreach (var item in await catalogue.EnrollmentsAsync(ct)) await Run(item.School,item.Enrollment,service.ReconcileAsync);
            }
            catch (Exception e) when (!ct.IsCancellationRequested) { logger.LogError("Document worker persistence unavailable: {ExceptionType}",e.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromSeconds(5),ct); } catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
        }
    }
}
