using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Starts and drives <c>ApplicationImportExecutionProcess</c> of a sealed plan, as the root reactor and worker do.</summary>
static class ApplicationImportProcessSupport
{
    public const string ProcessName = "ApplicationImportExecutionProcess";

    /// <summary>Creates (if absent) the process record of a sealed plan with the canonical target list.</summary>
    public static async Task<ProcessRecord> StartAsync(IServiceProvider services, Uuid tenantId, Uuid batchId)
    {
        var reader = services.GetRequiredService<IAggregateReader>();
        var content = services.GetRequiredService<IImmutableContentStore>();
        var batch = await reader.HydrateAsync(new ApplicationImportBatch(tenantId, batchId));
        var sealedPlan = batch.Plan ?? throw new InvalidOperationException("The plan is not sealed.");
        var plan = await ApplicationImportContent.ReadAsync<ImportFrozenPlanIntent>(content, tenantId,
            sealedPlan.FrozenPlan, CancellationToken.None);
        var groups = await ApplicationImportContent.ReadSelectionAsync<ImportTargetGroupIntent>(content, tenantId,
            plan.TargetGroups, CancellationToken.None);
        var source = new ApplicationImportSource(tenantId, plan.SourceKey, plan.SourceNamespace).Stream;
        return await services.GetRequiredService<IProcessRecordStore>().CreateIfAbsentAsync(ProcessRecord.Start(
            ProcessKey.ForRoot(ProcessName, sealedPlan.Operation.RootOperationId,
                NativeFactReference.Of(batch.Stream, sealedPlan)), NativeFactReference.Of(batch.Stream, sealedPlan),
            sealedPlan.Operation.Episode, sealedPlan.FrozenPlan,
            [$"{source.Area}/{source.Resource}", .. groups.Select(group => $"applications/{group.ApplicationId}")],
            DateTimeOffset.UtcNow));
    }

    /// <summary>Starts (if absent) and drives the process in a fresh scope, as a worker would.</summary>
    public static async Task<ProcessRecord> RunAsync(IServiceProvider provider, IServiceProvider scopeServices,
        Uuid tenantId, Uuid batchId)
    {
        var started = await StartAsync(scopeServices, tenantId, batchId);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ProcessDriver>().RunAsync(started.Key, "test-worker",
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
    }
}
