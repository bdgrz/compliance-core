using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Events;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Processes;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks.Reactors;

/// <summary>
///     Starts <c>RiskActionCompletionReviewProcess</c> from a committed review acceptance. The process record, with the
///     complete canonical evidence request key list of the frozen selection, exists before the source checkpoint
///     advances; the sweep resumes it if this reactor stops. A selection that cannot be read yet is transient: no record
///     is created from an unverified review.
/// </summary>
sealed partial class RiskActionCompletionReviewReactor(IProjectionCheckpointStore checkpoints,
    ProcessRootReaction process, IImmutableContentStore content, RiskActionReviewVerifier verifier)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(RiskTreatmentAction.Area), WorkloadName),
        IReactorHandler<RiskTreatmentActionCompletionReviewAccepted>
{
    public const string WorkloadName = "RiskActionCompletionReviewV1";

    public async ValueTask HandleAsync(IReactorContext<RiskTreatmentActionCompletionReviewAccepted> context,
        CancellationToken ct)
    {
        var accepted = context.Trigger;
        var verified = await verifier.VerifyAcceptedAsync(NativeFactReference.Of(context.Source.Stream, accepted),
            accepted.Intent, accepted.Operation.RootOperationId, accepted.Operation.LogicalId, ct).ConfigureAwait(false);
        if (verified.Error is not null)
            throw new ReactionCommandFailedException(verified.Error);
        var tenant = Uuid.Parse(context.Source.Stream.Realm, System.Globalization.CultureInfo.InvariantCulture);
        IReadOnlyList<ReviewedEvidenceRequest> cited;
        RiskActionCompletionReviewIntent intent;
        RiskActionReviewDecision decision;
        try
        {
            intent = await FindingContent.ReadAsync<RiskActionCompletionReviewIntent>(content, tenant, accepted.Intent,
                ct).ConfigureAwait(false);
            decision = await FindingContent.ReadAsync<RiskActionReviewDecision>(content, tenant, intent.AuthoredReview,
                ct).ConfigureAwait(false);
            cited = await ImmutableSelectionManifests.ReadAsync<ReviewedEvidenceRequest>(content, tenant.ToString(),
                intent.CompletionEvidence, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                $"The frozen review cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
        }

        _ = await process.StartAsync(RiskActionReviewEffects.ProcessName, context, accepted.Intent,
            cited.Select(item => RiskActionReviewEffects.EvidenceKey(tenant, decision.ProgramId, item.EvidenceRequestId))
                .OrderBy(static key => key, Kernel.StreamKeyOrder.Instance),
            $"reactor:{WorkloadName}", ct).ConfigureAwait(false);
    }
}
