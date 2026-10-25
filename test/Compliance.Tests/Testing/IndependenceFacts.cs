using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Drives admitted client independence previews through their process, as the reactor and sweep do.</summary>
static class IndependenceFacts
{
    /// <summary>Creates (if absent) the process record of an admitted evaluation without dispatching its receiver.</summary>
    public static async Task<ProcessRecord> StartPreviewProcessAsync(IServiceProvider provider, Uuid tenantId,
        Uuid evaluationId)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var owner = await services.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new ClientServiceIndependence(tenantId, evaluationId, evaluationId));
        var admitted = owner.AdmissionOf(evaluationId) ?? throw new InvalidOperationException("Admit first.");
        var key = ProcessKey.ForRoot(ClientIndependencePreviewEffects.ProcessName, evaluationId,
            NativeFactReference.Of(owner.Stream, admitted));
        return await services.GetRequiredService<IProcessRecordStore>().CreateIfAbsentAsync(ProcessRecord.Start(key,
            NativeFactReference.Of(owner.Stream, admitted), admitted.Operation.Episode, admitted.Intent, [],
            DateTimeOffset.UtcNow));
    }

    /// <summary>Starts (if absent) and drives the preview process of an admitted evaluation to its end.</summary>
    public static async Task<ProcessRecord> RunPreviewProcessAsync(IServiceProvider provider, Uuid tenantId,
        Uuid evaluationId)
    {
        var started = await StartPreviewProcessAsync(provider, tenantId, evaluationId);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ProcessDriver>().RunAsync(started.Key, "test-worker",
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
    }
}
