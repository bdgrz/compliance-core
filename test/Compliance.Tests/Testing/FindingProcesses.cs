using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.History;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Content.Persistence;
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
