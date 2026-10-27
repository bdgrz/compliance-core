using System.Globalization;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.Applications.Processes;

/// <summary>
///     The ordered application import process. The root is the sealed plan; the targets are the canonical stream keys
///     of the source and of every frozen target group, so every per-target stage walks the keys in canonical unsigned
///     UTF-8 order regardless of the authored row order: claim the source, reserve every target group before any
///     apply (driven by the execution authorization), apply one preflighted native batch per group, publish every
///     target together with the single commit, then release the targets and the source. A cancellation fences every
///     target (including targets whose hold was never acquired) before any release; the abort receiver is the
///     cancellation itself, so the factory builds no command for it.
/// </summary>
static class ApplicationImportExecutionProcess
{
    public const string Claim = "claim";
    public const string Reserve = "reserve";
    public const string Apply = "apply";
    public const string Publish = "publish";
    public const string Settle = "settle";

    public static ProcessDefinition Definition() => new(ApplicationImportEffects.ProcessName,
        [nameof(ApplicationImportPlanSealed)], true,
        [
            new ProcessStageDefinition(Claim, [nameof(ClaimApplicationImportSource)]),
            new ProcessStageDefinition(Reserve, [nameof(ReserveApplicationImportTarget)],
                "Every target group in canonical order before any apply; driven by ApplicationImportExecutionAuthorized."),
            new ProcessStageDefinition(Apply, [nameof(ApplyApplicationImportEffect)],
                "One preflighted native batch per target group.", PerTarget: true),
            new ProcessStageDefinition(Publish, [nameof(CommitApplicationImportBatch)], "One publication gate."),
            new ProcessStageDefinition(Settle,
                [nameof(ReleaseApplicationImportTarget), nameof(ReleaseApplicationImportSource)]),
        ], [nameof(CancelApplicationImport)], Receiver);

    /// <summary>The command id the process derives for the execution authorization of a sealed plan.</summary>
    public static Uuid ExecuteCommandId(ProcessRecord record) => EffectIdentity.CommandId(record.Source.EventId,
        $"{ApplicationImportEffects.ProcessName}:{nameof(ExecuteApplicationImport)}", "-", record.Episode);

    static async ValueTask<IRequest<ReceiverOutcome>?> Receiver(ProcessStep step, IServiceProvider services,
        CancellationToken ct)
    {
        var record = step.Record;
        if (step.Receiver == nameof(CancelApplicationImport) || step.Target is null &&
            step.Receiver != nameof(CommitApplicationImportBatch))
            return null;
        var realm = record.Key.Realm;
        var tenant = Uuid.Parse(realm, CultureInfo.InvariantCulture);
        var root = record.Key.RootOperationId;
        var store = services.GetRequiredService<IImmutableContentStore>();
        var reader = services.GetRequiredService<IAggregateReader>();
        var key = step.Target is null ? null : ApplicationImportEffects.ParseKey(step.Target);
        if (key is null && step.Receiver != nameof(CommitApplicationImportBatch))
            throw new InvalidOperationException($"{step.Target} is not an import process target.");
        ImportFrozenPlanIntent plan;
        try
        {
            plan = await ApplicationImportContent.ReadAsync<ImportFrozenPlanIntent>(store, tenant, record.Intent, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new SourceFactVerificationException($"The frozen import plan cannot be verified yet: {exception.Message}");
        }

        var isSource = key?.Area == ApplicationImportEffects.SourceArea;
        var isTarget = key?.Area == ApplicationImportEffects.TargetArea;
        var batch = await reader.HydrateAsync(new ApplicationImportBatch(tenant, plan.BatchId), ct).ConfigureAwait(false);
        var batchStream = batch.Stream;
        switch (step.Receiver)
        {
            case nameof(ClaimApplicationImportSource):
                return !isSource
                    ? null
                    : new ClaimApplicationImportSource(step.CommandId, record.Source,
                        new ApplicationImportSource(tenant, plan.SourceKey, plan.SourceNamespace).Stream, root,
                        ApplicationImportEffects.Generation, plan.SourceRevision, record.Intent, plan.BatchId,
                        plan.SourceKey, plan.SourceNamespace);
            case nameof(ReserveApplicationImportTarget) or nameof(ApplyApplicationImportEffect):
                {
                    if (!isTarget)
                        return null;
                    var authorized = await EnsureAuthorizedAsync(services, record, tenant, batch, ct).ConfigureAwait(false);
                    var group = await GroupAsync(services, tenant, record, key!.Value.Resource, ct).ConfigureAwait(false);
                    var groupRef = await ApplicationImportPlanWriter.GroupRefAsync(store, tenant, record.Intent, group, ct)
                        .ConfigureAwait(false);
                    var target = new DeclaredApplication(tenant, key.Value.Resource).Stream;
                    return step.Receiver == nameof(ReserveApplicationImportTarget)
                        ? new ReserveApplicationImportTarget(step.CommandId, authorized, target, root,
                            ApplicationImportEffects.Generation, null, groupRef, plan.BatchId, key.Value.Resource,
                            group.Rows.Content)
                        : new ApplyApplicationImportEffect(step.CommandId, authorized, target, root,
                            ApplicationImportEffects.Generation, null, groupRef, plan.BatchId, key.Value.Resource,
                            group.Rows.Content);
                }
            case nameof(CommitApplicationImportBatch):
                {
                    var authorized = await EnsureAuthorizedAsync(services, record, tenant, batch, ct).ConfigureAwait(false);
                    var manifest = await ProofManifestAsync(services, tenant, record, plan, ct).ConfigureAwait(false);
                    return new CommitApplicationImportBatch(step.CommandId, authorized, batchStream, root,
                        ApplicationImportEffects.Generation, null, record.Intent, plan.BatchId, manifest);
                }
            case nameof(ReleaseApplicationImportTarget) or nameof(ReleaseApplicationImportSource):
                {
                    var terminal = ApplicationImportVerifier.Terminal(batch);
                    if (terminal is null || terminal.Value.Fact.Metadata is null)
                        throw new SourceFactVerificationException("The terminal batch fact is not visible yet.");
                    var source = NativeFactReference.Of(batchStream, terminal.Value.Fact);
                    var intent = batch.Canceled?.Intent ?? record.Intent;
                    var operation = batch.Canceled is not null
                        ? await CancellationRootAsync(store, tenant, intent, ct).ConfigureAwait(false)
                        : root;
                    if (step.Receiver == nameof(ReleaseApplicationImportTarget))
                        return !isTarget
                            ? null
                            : new ReleaseApplicationImportTarget(step.CommandId, source,
                                new DeclaredApplication(tenant, key!.Value.Resource).Stream, operation,
                                ApplicationImportEffects.Generation, null, intent, plan.BatchId, key.Value.Resource);
                    return !isSource
                        ? null
                        : new ReleaseApplicationImportSource(step.CommandId, source,
                            new ApplicationImportSource(tenant, plan.SourceKey, plan.SourceNamespace).Stream, operation,
                            ApplicationImportEffects.Generation, null, intent, plan.BatchId);
                }
            default:
                throw new InvalidOperationException($"{step.Receiver} is not an application import receiver.");
        }
    }

    /// <summary>
    ///     The reserve stage is driven by the execution authorization: the process asks the batch to authorize
    ///     execution (an exact retry replays) and then proves its fact. A canceled batch authorizes nothing, so the
    ///     step waits and the abort takes over.
    /// </summary>
    static async ValueTask<NativeFactReference> EnsureAuthorizedAsync(IServiceProvider services, ProcessRecord record,
        Uuid tenant, ApplicationImportBatch batch, CancellationToken ct)
    {
        var reader = services.GetRequiredService<IAggregateReader>();
        if (batch.Authorization is null)
        {
            var bus = services.GetRequiredService<IRequestBus>();
            var executed = await bus.DispatchAsync(new ExecuteApplicationImport(ExecuteCommandId(record), tenant, batch.Id),
                new RequestDispatchContext(RequestActor.System), ct).ConfigureAwait(false);
            if (!executed.IsSuccess)
                throw new SourceFactVerificationException(
                    $"The execution authorization failed: {executed.Error!.Message}");
            batch = await reader.HydrateAsync(new ApplicationImportBatch(tenant, batch.Id), ct).ConfigureAwait(false);
        }

        return batch.Authorization?.Metadata is null
            ? throw new SourceFactVerificationException("The execution authorization is not visible yet.")
            : NativeFactReference.Of(batch.Stream, batch.Authorization);
    }

    static async ValueTask<ImportTargetGroupIntent> GroupAsync(IServiceProvider services, Uuid tenant,
        ProcessRecord record, Uuid applicationId, CancellationToken ct)
    {
        var store = services.GetRequiredService<IImmutableContentStore>();
        try
        {
            var plan = await ApplicationImportContent.ReadAsync<ImportFrozenPlanIntent>(store, tenant, record.Intent, ct)
                .ConfigureAwait(false);
            var groups = await ApplicationImportContent.ReadSelectionAsync<ImportTargetGroupIntent>(store, tenant,
                plan.TargetGroups, ct).ConfigureAwait(false);
            return groups.SingleOrDefault(group => group.ApplicationId == applicationId) ??
                throw new InvalidOperationException($"{applicationId} is not a frozen target group.");
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new SourceFactVerificationException($"The frozen target groups cannot be verified yet: {exception.Message}");
        }
    }

    /// <summary>
    ///     The native proof of every frozen row, read from the target streams in canonical group order. A missing or
    ///     not yet durable effect waits; the manifest is deterministic, so a retry writes the same content.
    /// </summary>
    static async ValueTask<ImmutableContentRef> ProofManifestAsync(IServiceProvider services, Uuid tenant,
        ProcessRecord record, ImportFrozenPlanIntent plan, CancellationToken ct)
    {
        var store = services.GetRequiredService<IImmutableContentStore>();
        var reader = services.GetRequiredService<IAggregateReader>();
        var proofs = new List<ApplicationImportEffectProof>();
        try
        {
            var groups = await ApplicationImportContent.ReadSelectionAsync<ImportTargetGroupIntent>(store, tenant,
                plan.TargetGroups, ct).ConfigureAwait(false);
            foreach (var group in groups)
            {
                var rows = await ApplicationImportContent.ReadSelectionAsync<ApplicationImportPlannedRow>(store, tenant,
                    group.Rows, ct).ConfigureAwait(false);
                var target = await reader.HydrateAsync(new DeclaredApplication(tenant, group.ApplicationId), ct)
                    .ConfigureAwait(false);
                var hold = target.FindImportHold(plan.BatchId);
                foreach (var row in rows)
                {
                    var effect = hold?.Effects.FirstOrDefault(candidate => candidate.Row.RowId == row.RowId);
                    if (effect?.Metadata is not { } metadata || target.CommittedStreamPosition < metadata.AggregateVersion)
                        throw new SourceFactVerificationException(
                            "A target effect is not visible or durable yet; the batch waits to publish.");
                    proofs.Add(new ApplicationImportEffectProof(row.RowId, group.ApplicationId, metadata.EventId,
                        metadata.AggregateVersion));
                }
            }

            return await store.WriteJsonAsync<IReadOnlyList<ApplicationImportEffectProof>>(ApplicationImportContent.Realm(tenant),
                Uuid.CreateVersion5(record.Intent.ContentId, "import-effects-manifest"), "import-effects-manifest.v1",
                proofs, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new SourceFactVerificationException($"The frozen plan cannot be verified yet: {exception.Message}");
        }
    }

    static async ValueTask<Uuid> CancellationRootAsync(IImmutableContentStore store, Uuid tenant,
        ImmutableContentRef intent, CancellationToken ct)
    {
        try
        {
            return (await ApplicationImportContent.ReadAsync<ImportFrozenPlanIntent>(store, tenant, intent, ct)
                .ConfigureAwait(false)).Header.RootOperationId;
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new SourceFactVerificationException($"The cancellation cannot be verified yet: {exception.Message}");
        }
    }
}
