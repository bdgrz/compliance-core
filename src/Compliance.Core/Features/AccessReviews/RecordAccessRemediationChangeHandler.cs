using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Records a provider-side change as the root native fact of the item; it never counts as platform verification.
///     The frozen first response is the complete change.
/// </summary>
public sealed class RecordAccessRemediationChangeHandler(NativeOperationExecutor operations,
    AccessReviewReader campaigns, IImmutableContentStore content, TimeProvider clock,
    RestrictedApplicationVisibility visibility, AccessReviewQueueEligibility eligibility)
    : IRequestHandler<RecordAccessRemediationChange, AccessRemediationChangeView>
{
    const string CommandKind = nameof(RecordAccessRemediationChange);

    public async ValueTask<Result<AccessRemediationChangeView>> HandleAsync(
        IRequestContext<RecordAccessRemediationChange> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ExpectedRevision < 0)
            return AccessReviewOutcome.Failure<AccessRemediationChangeView>(RequestErrorKind.Validation,
                "A change requires a command id and a non-negative expected revision.");
        var actor = AccessReviewActor.From(context);
        var read = await campaigns.ReadItemAsync(request.TenantId, request.CampaignId, request.ItemId,
            request.CommandId, ct).ConfigureAwait(false);
        if (read.State is not { } state || read.Frozen is not { } frozen || read.Campaign.Launched is not { } launched ||
            !await visibility.CanReadSystemInstanceAsync(request.TenantId, actor.UserId, frozen.SystemInstanceId, ct)
                .ConfigureAwait(false))
            return AccessReviewOutcome.Failure<AccessRemediationChangeView>(RequestErrorKind.NotFound,
                "The review item was not found.");

        var now = clock.GetUtcNow();
        var provenance = AccessReviewItemWrites.Provenance(read, request.CommandId, CommandKind, actor.Reference,
            request.ExpectedRevision, AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);
        var decided = read.Item.Acceptance.Decide(binding);
        if (decided != NativeAcceptanceDecision.Replayed && launched.ProgramId is { } programId &&
            (state.CurrentRemediationOwnerMemberId != actor.MemberId ||
             !await eligibility.CanRemediateAsync(request.TenantId, programId, actor.MemberId,
                 frozen.SystemInstanceId, ct).ConfigureAwait(false)))
            return AccessReviewOutcome.Failure<AccessRemediationChangeView>(RequestErrorKind.NotFound,
                "The review item was not found among the remediation owner's current queue access.");

        FirstResponseDescriptor? response = null;
        ImmutableContentRef? change = null;
        ImmutableContentRef? result = null;
        if (decided == NativeAcceptanceDecision.None)
        {
            if (read.Item.PrepareChange(request.ExpectedRevision) is { IsSuccess: false } blocked)
                return Result<AccessRemediationChangeView>.Failure(blocked.Error!);
            if (AccessReviewRules.CheckChange(state, request.Reference, request.Description, request.ChangedAt, now)
                is { } failure)
                return Result<AccessRemediationChangeView>.Failure(failure);
            var view = new AccessRemediationChangeView(request.CommandId, request.Reference.Trim(),
                request.Description.Trim(), request.ChangedAt, actor.Reference, now);
            change = await AccessReviewContent.WriteAsync(content, request.TenantId, AccessReviewContent.ChangeSchema,
                view, ct).ConfigureAwait(false);
            (response, result) = await AccessReviewContent.FreezeOkAsync(content, request.TenantId,
                AccessReviewContent.ChangeViewSchema, view, ct).ConfigureAwait(false);
        }

        var observed = state.Revision;
        var outcome = await operations.ExecuteAsync(new NativeOperation<AccessReviewItem>(binding,
            () => new AccessReviewItem(request.TenantId, request.CampaignId, request.ItemId, request.CommandId),
            owner => owner.Acceptance,
            owner => AccessReviewItemWrites.Prepare(owner, request.ExpectedRevision, observed, response),
            (AccessReviewItem owner, ImmutableContentRef frozenResponse) => owner.RecordChange(provenance, change!,
                frozenResponse, result!)), context, ct).ConfigureAwait(false);
        return await AccessReviewContent.ResponseAsync<AccessRemediationChangeView>(content, request.TenantId,
            outcome, ct).ConfigureAwait(false);
    }
}
