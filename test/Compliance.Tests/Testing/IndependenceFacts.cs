using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
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
