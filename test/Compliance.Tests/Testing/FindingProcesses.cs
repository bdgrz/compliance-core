using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Starts and drives the finding processes of a composed provider exactly as their root reactors and the process
///     worker would: the process record is created from the committed root fact, then the driver runs it.
/// </summary>
static class FindingProcesses
{
    public static Task<ProcessKey> MaterializationKey(IServiceProvider provider, Uuid tenantId, Uuid rootOperationId) =>
        ProcessKeys.FindAsync(provider, FindingClosureEffects.MaterializationProcessName, tenantId.ToString(), rootOperationId);

    public static Task<ProcessKey> ClosureKey(IServiceProvider provider, Uuid tenantId, Uuid commandId) =>
        ProcessKeys.FindAsync(provider, FindingClosureEffects.ProcessName, tenantId.ToString(), commandId);

    /// <summary>Starts and drives the materialization process of every admitted action of the finding.</summary>
    public static async Task<IReadOnlyList<ProcessRecord>> MaterializeAsync(IServiceProvider provider, Uuid tenantId,
        Uuid programId, Uuid findingId, CancellationToken ct = default)
    {
        await using var scope = provider.CreateAsyncScope();
        var finding = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new FindingAggregate(tenantId, programId, findingId), ct);
        var records = scope.ServiceProvider.GetRequiredService<IProcessRecordStore>();
        var driven = new List<ProcessRecord>();
        var history = await FindingHistory.ReadAsync(scope.ServiceProvider.GetRequiredService<IDomainEventReader>(),
            finding, ct);
        foreach (var admitted in history.Admitted)
        {
            var key = ProcessKey.ForRoot(FindingClosureEffects.MaterializationProcessName, admitted.Operation.RootOperationId,
                NativeFactReference.Of(finding.Stream, admitted));
            await records.CreateIfAbsentAsync(ProcessRecord.Start(key, NativeFactReference.Of(finding.Stream, admitted),
                admitted.Operation.Episode, admitted.Intent, [admitted.Operation.LogicalId.ToString()],
                DateTimeOffset.UtcNow), ct);
            driven.Add(await scope.ServiceProvider.GetRequiredService<ProcessDriver>().RunAsync(key, "test-worker",
                new RequestDispatchContext(RequestActor.System), ct));
        }

        return driven;
    }

    /// <summary>Starts (if absent) the closure process of an accepted closure.</summary>
    public static async Task<ProcessRecord> StartClosureAsync(IServiceProvider provider, Uuid tenantId, Uuid programId,
        Uuid findingId, Uuid commandId, CancellationToken ct = default)
    {
        await using var scope = provider.CreateAsyncScope();
        var finding = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new FindingAggregate(tenantId, programId, findingId, selectedClosureRoot: commandId), ct);
        var accepted = finding.AcceptedClosure(commandId) ?? throw new InvalidOperationException("Not accepted.");
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var intent = await content.ReadJsonAsync<ClosureIntent>(tenantId.ToString(), accepted.Intent, ct);
        var children = await ImmutableSelectionManifests.ReadAsync<AdmittedChild>(content, tenantId.ToString(),
            intent.AdmittedChildren, ct);
        return await scope.ServiceProvider.GetRequiredService<IProcessRecordStore>().CreateIfAbsentAsync(
            ProcessRecord.Start(ProcessKey.ForRoot(FindingClosureEffects.ProcessName, commandId,
                NativeFactReference.Of(finding.Stream, accepted)), NativeFactReference.Of(finding.Stream, accepted),
                accepted.Operation.Episode, accepted.Intent,
                children.Select(child => FindingClosureEffects.ActionKey(tenantId, programId, findingId, child.ChildId)),
                DateTimeOffset.UtcNow), ct);
    }

    /// <summary>Drives the closure process of an accepted closure until it is terminal or waits.</summary>
    public static async Task<ProcessRecord> RunClosureAsync(IServiceProvider provider, Uuid tenantId, Uuid programId,
        Uuid findingId, Uuid commandId, CancellationToken ct = default)
    {
        var record = await StartClosureAsync(provider, tenantId, programId, findingId, commandId, ct);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ProcessDriver>().RunAsync(record.Key, "test-worker",
            new RequestDispatchContext(RequestActor.System), ct);
    }
}
