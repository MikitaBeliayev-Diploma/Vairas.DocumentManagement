using System.Globalization;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Vairas.DocumentManagement.Business;
using Microsoft.Extensions.Options;
using Vairas.DocumentManagement.Configuration.Options;

namespace Vairas.DocumentManagement.Integrations;


public sealed class DocumentInboxWorker(IServiceScopeFactory scopes, IOptions<DocumentBusOptions> optionsSettings, ILogger<DocumentInboxWorker> logger) : BackgroundService
{
    private readonly DocumentBusOptions options = optionsSettings.Value;
    public static SourceEvent Decode(ServiceBusReceivedMessage message, string producer)
    {
        string Header(string key) => message.ApplicationProperties.TryGetValue(key,out var value) ? Convert.ToString(value,CultureInfo.InvariantCulture) ?? "" : "";
        if (message.Body.ToMemory().Length > 65536 || !Guid.TryParse(Header("eventId"),out var id) || message.MessageId != id.ToString("D") || message.Subject != Header("eventType") ||
            Header("producer") != producer || !int.TryParse(Header("eventVersion"),out var version) || !Guid.TryParse(Header("aggregateId"),out var aggregate) || !long.TryParse(Header("aggregateVersion"),out var revision) ||
            !DateTimeOffset.TryParse(Header("occurredAt"),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out var at)) throw DocumentErrors.Invalid("INVALID_EVENT_ENVELOPE");
        using var body = JsonDocument.Parse(message.Body.ToString(),new JsonDocumentOptions { MaxDepth = 16 });
        var school = body.RootElement.GetProperty("schoolId").GetGuid(); var enrollment = body.RootElement.GetProperty("enrollmentId").GetGuid();
        if ((producer == "school" || message.Subject == "TrainingEvidenceInvalidated") && aggregate != enrollment) throw DocumentErrors.Invalid("INVALID_EVENT_AGGREGATE");
        if (message.Subject == "TrainingEvidenceInvalidated" && body.RootElement.GetProperty("dependencyRevision").GetInt64() != revision) throw DocumentErrors.Invalid("INVALID_EVENT_REVISION");
        return new(id,producer,message.Subject,version,school,enrollment,aggregate,revision,message.Body.ToString(),Header("correlationId"),at);
    }
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!options.Enabled) return;
        while (!ct.IsCancellationRequested)
        {
            try { await ReceiveAsync(ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception e) { logger.LogError("Document broker connection deferred: {ExceptionType}",e.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromSeconds(5),ct); } catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
        }
    }
    private async Task ReceiveAsync(CancellationToken ct)
    {
        await using var client = new ServiceBusClient(options.ConnectionString); var processors = new List<ServiceBusProcessor>();
        try
        {
            foreach (var pair in new[] { (options.LearningTopic,"learning"), (options.SchoolTopic,"school") })
            {
                var processor = client.CreateProcessor(pair.Item1,options.Subscription,new() { AutoCompleteMessages = false, MaxConcurrentCalls = 4, MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(5) });
                processor.ProcessMessageAsync += async args =>
                {
                    try
                    {
                        var m = Decode(args.Message,pair.Item2); using var log = logger.BeginScope(new Dictionary<string,object> { ["CorrelationId"] = m.CorrelationId, ["EventId"] = m.EventId });
                        using var scope = scopes.CreateScope(); await scope.ServiceProvider.GetRequiredService<DocumentService>().ConsumeAsync(m,args.CancellationToken);
                        await args.CompleteMessageAsync(args.Message,args.CancellationToken);
                    }
                    catch (Exception e) when (!args.CancellationToken.IsCancellationRequested)
                    {
                        logger.LogWarning("Document event deferred: {ExceptionType}",e.GetType().Name);
                        if (e is JsonException or KeyNotFoundException or FormatException || e is BadRequestException or ConflictException or UnprocessableEntityException || args.Message.DeliveryCount >= options.MaxDeliveryCount)
                            await args.DeadLetterMessageAsync(args.Message,"DocumentEventRejected","Inspect event identifiers and authoritative state before redrive.",args.CancellationToken);
                        else { await Task.Delay(TimeSpan.FromSeconds(Math.Min(30,Math.Pow(2,args.Message.DeliveryCount))),args.CancellationToken); await args.AbandonMessageAsync(args.Message,cancellationToken:args.CancellationToken); }
                    }
                };
                processor.ProcessErrorAsync += error => { logger.LogWarning("Document broker unavailable: {ExceptionType}",error.Exception.GetType().Name); return Task.CompletedTask; };
                processors.Add(processor); await processor.StartProcessingAsync(ct);
            }
            await Task.Delay(Timeout.InfiniteTimeSpan,ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        finally { foreach (var processor in processors) await processor.DisposeAsync(); }
    }
}
