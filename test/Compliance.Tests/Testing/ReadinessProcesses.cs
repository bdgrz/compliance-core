using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
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
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
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
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Readiness.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Starts and drives the readiness processes of a composed provider exactly as their root reactors and the process
///     worker would: the process record is created from the committed root fact, then the driver runs it.
/// </summary>
static class ReadinessProcesses
{
    /// <summary>Starts (if absent) the run process of an admitted run.</summary>
    public static async Task<ProcessRecord> StartRunAsync(IServiceProvider provider, Uuid tenantId, Uuid programId,
        Uuid assessmentId, CancellationToken ct = default)
    {
        await using var scope = provider.CreateAsyncScope();
        var program = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new ProgramReadiness(tenantId, programId, assessmentId), ct);
        var requested = program.RequestedRun(assessmentId) ?? throw new InvalidOperationException("Not admitted.");
        return await scope.ServiceProvider.GetRequiredService<IProcessRecordStore>().CreateIfAbsentAsync(
            ProcessRecord.Start(ProcessKey.ForRoot(ReadinessEffects.RunProcessName, assessmentId,
                NativeFactReference.Of(program.Stream, requested)), NativeFactReference.Of(program.Stream, requested),
                requested.Operation.Episode, requested.Intent, [], DateTimeOffset.UtcNow), ct);
    }

    /// <summary>Drives the run process of an admitted run until it is terminal or waits.</summary>
    public static async Task<ProcessRecord> RunRunAsync(IServiceProvider provider, Uuid tenantId, Uuid programId,
        Uuid assessmentId, CancellationToken ct = default)
    {
        var record = await StartRunAsync(provider, tenantId, programId, assessmentId, ct);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ProcessDriver>().RunAsync(record.Key, "test-worker",
            new RequestDispatchContext(RequestActor.System), ct);
    }

    /// <summary>Starts (if absent) the decision process of an accepted decision.</summary>
    public static async Task<ProcessRecord> StartDecisionAsync(IServiceProvider provider, Uuid tenantId, Uuid programId,
        Uuid commandId, CancellationToken ct = default)
    {
        await using var scope = provider.CreateAsyncScope();
        var program = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new ProgramReadiness(tenantId, programId, commandId), ct);
        var accepted = program.AcceptedDecision(commandId) ?? throw new InvalidOperationException("Not accepted.");
        var terms = ProgramReadiness.DecisionTerms(accepted)!.Value;
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var intent = await content.ReadJsonAsync<ReadinessDecisionIntent>(tenantId.ToString(), terms.Intent, ct);
        var targets = await ReadinessDecisionProcess.TargetsAsync(content, tenantId, intent, ct);
        return await scope.ServiceProvider.GetRequiredService<IProcessRecordStore>().CreateIfAbsentAsync(
            ProcessRecord.Start(ProcessKey.ForRoot(ReadinessEffects.DecisionProcessName, commandId,
                NativeFactReference.Of(program.Stream, accepted)), NativeFactReference.Of(program.Stream, accepted),
                ((INativeOperationFact)accepted).Operation.Episode, terms.Intent, targets, DateTimeOffset.UtcNow), ct);
    }

    /// <summary>Drives the decision process of an accepted decision until it is terminal or waits.</summary>
    public static async Task<ProcessRecord> RunDecisionAsync(IServiceProvider provider, Uuid tenantId, Uuid programId,
        Uuid commandId, CancellationToken ct = default)
    {
        var record = await StartDecisionAsync(provider, tenantId, programId, commandId, ct);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ProcessDriver>().RunAsync(record.Key, "test-worker",
            new RequestDispatchContext(RequestActor.System), ct);
    }

    /// <summary>The receiver command a step of a record builds, with the deterministic command id of the driver.</summary>
    public static async Task<IRequest<ReceiverOutcome>?> StepAsync(IServiceProvider provider, string processName,
        ProcessRecord record, string stage, string receiver, string? target)
    {
        await using var scope = provider.CreateAsyncScope();
        var definition = scope.ServiceProvider.GetRequiredService<ProcessCatalog>().Get(processName);
        return await definition.CreateReceiver(new ProcessStep(record, stage, receiver, target,
            Bdgrz.Compliance.Features.Kernel.Services.EffectIdentity.CommandId(record.Source.EventId, $"{processName}:{receiver}",
                target ?? "-", record.Episode)), scope.ServiceProvider, CancellationToken.None);
    }

    /// <summary>Dispatches a receiver as the trusted process principal, in its own scope.</summary>
    public static async Task<Result<ReceiverOutcome>> SendReceiverAsync(IServiceProvider provider,
        IRequest<ReceiverOutcome> command)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(command,
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
    }
}
