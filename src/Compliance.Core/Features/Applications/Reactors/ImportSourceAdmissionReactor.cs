using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
///     Starts <c>ApplicationImportExecutionProcess</c> from a committed plan seal. The process record, with the
///     canonical target list of the source and every frozen group, exists before the source checkpoint advances; the
///     sweep resumes it if this reactor stops. A plan that cannot be read yet is transient: no record is created from
///     an unverified plan.
/// </summary>
sealed partial class ImportSourceAdmissionReactor(IProjectionCheckpointStore checkpoints, ProcessRootReaction process,
    IImmutableContentStore content, ISourceFactResolver sources)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(ApplicationImportBatch.Area), WorkloadName),
        IReactorHandler<ApplicationImportPlanSealed>
{
    public const string WorkloadName = "ImportSourceAdmissionV1";

    public async ValueTask HandleAsync(IReactorContext<ApplicationImportPlanSealed> context, CancellationToken ct)
    {
        await ApplicationImportReactorSources.VerifyAsync(sources, content, Pattern, context, context.Trigger.Operation.Owner, ct)
            .ConfigureAwait(false);
        var sealedPlan = context.Trigger;
        var tenant = sealedPlan.TenantId;
        string[] targets;
        try
        {
            var plan = await ApplicationImportContent.ReadAsync<ImportFrozenPlanIntent>(content, tenant,
                sealedPlan.FrozenPlan, ct).ConfigureAwait(false);
            ApplicationImportIntentBindings.Require(plan.Header, sealedPlan.Operation);
            if (plan.BatchId != sealedPlan.BatchId || plan.SourceKey != sealedPlan.SourceKey ||
                plan.SourceNamespace != sealedPlan.SourceNamespace || plan.Rows.Count != sealedPlan.RowCount ||
                plan.TargetGroups.Count != sealedPlan.TargetGroupCount || sealedPlan.PlanSha256 != sealedPlan.FrozenPlan.Sha256)
                throw new ImmutableContentIntegrityException("The complete frozen plan differs from its admitted native batch scope and counts.");
            var groups = await ApplicationImportContent.ReadSelectionAsync<ImportTargetGroupIntent>(content, tenant,
                plan.TargetGroups, ct).ConfigureAwait(false);
            var source = new ApplicationImportSource(tenant, plan.SourceKey, plan.SourceNamespace).Stream;
            targets =
            [
                ApplicationImportEffects.SourceKey(source),
                .. groups.Select(group => ApplicationImportEffects.TargetKey(group.ApplicationId)),
            ];
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                $"The frozen plan cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
        }

        _ = await process.StartAsync(ApplicationImportEffects.ProcessName, context, sealedPlan.FrozenPlan, targets,
            $"reactor:{WorkloadName}", ct).ConfigureAwait(false);
    }
}
