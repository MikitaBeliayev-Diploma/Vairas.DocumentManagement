using Azure.Messaging.ServiceBus;
using Vairas.DocumentManagement.Business;
using Vairas.DocumentManagement.Integrations;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vairas.DocumentManagement.Configuration.Options;
namespace Vairas.DocumentManagement.Tests;

public sealed class EventContractTests
{
    [Fact] public async Task Broker_bootstrap_failure_is_retried_without_faulting_the_host()
    {
        using var provider = new ServiceCollection().BuildServiceProvider(); var logger = new FailureLogger();
        using var worker = new DocumentInboxWorker(provider.GetRequiredService<IServiceScopeFactory>(),Microsoft.Extensions.Options.Options.Create(new DocumentBusOptions { Enabled = true, ConnectionString = "invalid-test-configuration" }),logger);
        await worker.StartAsync(default); await logger.Failed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(worker.ExecuteTask!.IsFaulted); await worker.StopAsync(default);
    }
    [Fact] public void Broker_envelope_is_bound_to_producer_event_identity_and_revision()
    {
        var id = Guid.NewGuid(); var enrollment = Guid.NewGuid(); var school = Guid.NewGuid();
        var headers = new Dictionary<string,object> { ["eventId"] = id.ToString("D"),["eventType"] = "TrainingEvidenceInvalidated",["producer"] = "learning",["eventVersion"] = 1,
            ["aggregateId"] = enrollment.ToString("D"),["aggregateVersion"] = 3L,["occurredAt"] = "2026-11-24T12:00:00Z",["correlationId"] = "contract-fixture" };
        var body = BinaryData.FromString(Wire.Save(new { schoolId = school, enrollmentId = enrollment, dependencyRevision = 3, reason = "REVISED" }));
        var valid = ServiceBusModelFactory.ServiceBusReceivedMessage(body,messageId:id.ToString("D"),subject:"TrainingEvidenceInvalidated",properties:headers);
        Assert.Equal(enrollment,DocumentInboxWorker.Decode(valid,"learning").EnrollmentId);
        Business.ThrowsBusiness(() => DocumentInboxWorker.Decode(valid,"school"));
        var conflict = ServiceBusModelFactory.ServiceBusReceivedMessage(body,messageId:Guid.NewGuid().ToString("D"),subject:"TrainingEvidenceInvalidated",properties:headers);
        Business.ThrowsBusiness(() => DocumentInboxWorker.Decode(conflict,"learning"));
    }
    private sealed class FailureLogger : ILogger<DocumentInboxWorker>
    {
        public TaskCompletionSource Failed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level,EventId id,TState state,Exception? exception,Func<TState,Exception?,string> formatter) { if (level == LogLevel.Error) Failed.TrySetResult(); }
    }
}
