using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.History;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Processes;
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
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.Applications.Reactors;

/// <summary>
///     Carries an accepted cancellation to its end. The batch's own cancellation fact already blocks a later commit;
///     here every target of the sealed plan is fenced (including targets whose hold was never acquired) so a delayed
///     reserve or apply meets the fence, the cancellation completes once the manifest proves every fence, and only
///     then are the target holds and the source claim released. The release commands carry the identities the
///     process settle stage derives, so the process and this reactor converge on the same facts. The process, when it
///     exists, is aborted first so no forward stage starts again.
/// </summary>
sealed partial class ImportCancellationReactor(IProjectionCheckpointStore checkpoints, IRequestBus bus,
    IAggregateReader reader, IImmutableContentStore content, ProcessDriver driver, ISourceFactResolver sources)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(ApplicationImportBatch.Area), WorkloadName),
        IReactorHandler<ApplicationImportCancellationAccepted>
{
    public const string WorkloadName = "ImportCancellationV1";

    public async ValueTask HandleAsync(IReactorContext<ApplicationImportCancellationAccepted> context,
        CancellationToken ct)
    {
        await ApplicationImportReactorSources.VerifyAsync(sources, content, Pattern, context, context.Trigger.Operation.Owner, ct)
            .ConfigureAwait(false);
        var accepted = context.Trigger;
        var stream = context.Source.Stream;
        var tenant = Uuid.Parse(stream.Realm, System.Globalization.CultureInfo.InvariantCulture);
        var batchId = Uuid.Parse(stream.Resource!, System.Globalization.CultureInfo.InvariantCulture);
        var batch = await reader.HydrateAsync(new ApplicationImportBatch(tenant, batchId), ct).ConfigureAwait(false);
        if (!batch.IsCreated || batch.CancellationAccepted?.Metadata?.EventId != accepted.Metadata.EventId)
            throw Transient("The accepted cancellation is not visible on its batch yet.");
        if (batch.Committed is not null)
            return;
        ImportFrozenPlanIntent cancellation;
        IReadOnlyList<ImportTargetGroupIntent> groups = [];
        try
        {
            cancellation = await ApplicationImportContent.ReadAsync<ImportFrozenPlanIntent>(content, tenant,
                accepted.Intent, ct).ConfigureAwait(false);
            ApplicationImportIntentBindings.Require(cancellation.Header, accepted.Operation);
            if (cancellation.BatchId != batchId || cancellation.SourceKey != batch.SourceKey ||
                cancellation.SourceNamespace != batch.SourceNamespace)
                throw new ImmutableContentIntegrityException("The cancellation intent belongs to another batch source identity.");
            if (batch.Plan is { } sealedPlan)
            {
                var plan = await ApplicationImportContent.ReadAsync<ImportFrozenPlanIntent>(content, tenant,
                    sealedPlan.FrozenPlan, ct).ConfigureAwait(false);
                groups = await ApplicationImportContent.ReadSelectionAsync<ImportTargetGroupIntent>(content, tenant,
                    plan.TargetGroups, ct).ConfigureAwait(false);
            }
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                $"The cancellation cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
        }

        if (batch.Plan is { } sealedFact)
        {
            try
            {
                _ = await driver.AbortAsync(ProcessKey.ForRoot(ApplicationImportEffects.ProcessName, sealedFact.Operation.RootOperationId,
                    NativeFactReference.Of(stream, sealedFact)), ct).ConfigureAwait(false);
            }
            catch (KeyNotFoundException)
            {
                // The root reactor has not created the record yet; nothing was started, so there is nothing to stop.
            }
        }

        var source = NativeFactReference.Of(stream, accepted);
        var root = cancellation.Header.RootOperationId;
        var episode = accepted.Operation.Episode;
        var fences = new List<ApplicationImportFenceProof>(groups.Count);
        foreach (var group in groups.OrderBy(group => ApplicationImportEffects.TargetKey(group.ApplicationId),
                     Bdgrz.Compliance.Features.Kernel.Services.StreamKeyOrder.Instance))
        {
            var groupRef = await ApplicationImportPlanWriter.GroupRefAsync(content, tenant, batch.Plan!.FrozenPlan,
                group, ct).ConfigureAwait(false);
            var target = new DeclaredApplication(tenant, group.ApplicationId).Stream;
            var fenced = await bus.SendAsync(new FenceApplicationImportTarget(ApplicationImportEffects.ReceiverCommandId(
                source.EventId, nameof(FenceApplicationImportTarget), ApplicationImportEffects.TargetKey(group.ApplicationId),
                episode), source, target, root, ApplicationImportEffects.Generation, null, groupRef, batchId,
                group.ApplicationId), context, ct).ConfigureAwait(false);
            if (!fenced.IsSuccess)
                throw new ReactionCommandFailedException(fenced.Error!);
            var fact = fenced.Value switch
            {
                ReceiverOutcome.Applied applied => applied.Fact,
                ReceiverOutcome.Replayed replayed => replayed.Fact,
                _ => throw Transient("The target was settled before its fence was recorded."),
            };
            fences.Add(new ApplicationImportFenceProof(group.ApplicationId, fact.EventId, fact.AggregateVersion));
        }

        var manifest = await content.WriteJsonAsync<IReadOnlyList<ApplicationImportFenceProof>>(
            ApplicationImportContent.Realm(tenant), Uuid.CreateVersion5(accepted.Intent.ContentId, "import-fence-manifest"),
            "import-fence-manifest.v1", fences, ct).ConfigureAwait(false);
        var completed = await bus.SendAsync(new CompleteApplicationImportCancellation(
            ApplicationImportEffects.ReceiverCommandId(source.EventId, nameof(CompleteApplicationImportCancellation),
                "-", episode), source, stream, root, ApplicationImportEffects.Generation, null, accepted.Intent, batchId,
            manifest), context, ct).ConfigureAwait(false);
        if (!completed.IsSuccess)
            throw new ReactionCommandFailedException(completed.Error!);
        if (completed.Value is ReceiverOutcome.Superseded)
            return;

        // Release only after the cancellation fact is committed and every fence is recorded.
        batch = await reader.HydrateAsync(new ApplicationImportBatch(tenant, batchId), ct).ConfigureAwait(false);
        if (batch.Canceled is not { Metadata: not null } canceled)
            throw Transient("The completed cancellation is not visible on its batch yet.");
        if (batch.Plan is not { } plan0)
            return;
        var terminal = NativeFactReference.Of(stream, canceled);
        foreach (var group in groups)
        {
            var released = await bus.SendAsync(new ReleaseApplicationImportTarget(ApplicationImportEffects.ReceiverCommandId(
                plan0.Metadata.EventId, nameof(ReleaseApplicationImportTarget),
                ApplicationImportEffects.TargetKey(group.ApplicationId), plan0.Operation.Episode), terminal,
                new DeclaredApplication(tenant, group.ApplicationId).Stream, root, ApplicationImportEffects.Generation,
                null, canceled.Intent, batchId, group.ApplicationId), context, ct).ConfigureAwait(false);
            if (!released.IsSuccess)
                throw new ReactionCommandFailedException(released.Error!);
        }

        var sourceStream = new ApplicationImportSource(tenant, batch.SourceKey!, batch.SourceNamespace!).Stream;
        var sourceReleased = await bus.SendAsync(new ReleaseApplicationImportSource(
            ApplicationImportEffects.ReceiverCommandId(plan0.Metadata.EventId, nameof(ReleaseApplicationImportSource),
                ApplicationImportEffects.SourceKey(sourceStream), plan0.Operation.Episode), terminal, sourceStream, root,
            ApplicationImportEffects.Generation, null, canceled.Intent, batchId), context, ct).ConfigureAwait(false);
        if (!sourceReleased.IsSuccess)
            throw new ReactionCommandFailedException(sourceReleased.Error!);
    }

    static ReactionCommandFailedException Transient(string message) =>
        new(new RequestError(RequestErrorKind.Conflict, message, true));
}
