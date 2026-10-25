using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Records the assigned reviewer's own decision on one item as the root native fact of the item; HTTP-only, never
///     an MCP tool. The frozen first response is the complete decision, so an exact retry returns it even after later
///     changes.
/// </summary>
public sealed class RecordAccessDecisionHandler(NativeOperationExecutor operations, AccessReviewReader campaigns,
    IAggregateReader reader, IImmutableContentStore content, TimeProvider clock,
    RestrictedApplicationVisibility visibility, AccessReviewQueueEligibility eligibility)
    : IRequestHandler<RecordAccessDecision, AccessDecisionView>
{
    const string CommandKind = nameof(RecordAccessDecision);

    public async ValueTask<Result<AccessDecisionView>> HandleAsync(
        IRequestContext<RecordAccessDecision> context, CancellationToken ct)
    {
        try
        {
            return await HandleCoreAsync(context, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<AccessDecisionView>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The exact access item source cannot be verified: {error.Message}", error is ImmutableContentUnavailableException));
        }
    }

    async ValueTask<Result<AccessDecisionView>> HandleCoreAsync(
        IRequestContext<RecordAccessDecision> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return AccessReviewOutcome.Failure<AccessDecisionView>(RequestErrorKind.Forbidden,
                "Access review sign-off requires personal HTTP submission.");
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ExpectedRevision < 0)
            return AccessReviewOutcome.Failure<AccessDecisionView>(RequestErrorKind.Validation,
                "A decision requires a command id and a non-negative expected revision.");
        var actor = AccessReviewActor.From(context);
        var read = await campaigns.ReadItemIdentityAsync(request.TenantId, request.CampaignId, request.ItemId,
            request.CommandId, ct).ConfigureAwait(false);
        if (read.Frozen is not { } frozen || read.Campaign.Launched is not { } launched ||
            !await visibility.CanReadSystemInstanceAsync(request.TenantId, actor.UserId, frozen.SystemInstanceId, ct)
                .ConfigureAwait(false))
            return AccessReviewOutcome.Failure<AccessDecisionView>(RequestErrorKind.NotFound,
                "The review item was not found.");

        var now = clock.GetUtcNow();
        var provenance = AccessReviewItemWrites.Provenance(read, request.CommandId, CommandKind, actor.Reference,
            request.ExpectedRevision, AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);
        FirstResponseDescriptor? response = null;
        AccessReviewPreparedCut? preparedCut = null;
        ImmutableContentRef? decisions = null;
        ImmutableContentRef? intent = null;
        ImmutableContentRef? result = null;
        var outcome = await operations.ExecuteAsync(new NativeOperation<AccessReviewItem>(binding,
            () => new AccessReviewItem(request.TenantId, request.CampaignId, request.ItemId, request.CommandId),
            owner => owner.Acceptance,
            async (owner, prepareCt) =>
            {
                var prepared = owner.PrepareChange(request.ExpectedRevision);
                if (!prepared.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
                var state = await campaigns.ReadStateAsync(request.TenantId, owner, frozen,
                    read.Campaign.Launched?.RemediationOwnerMemberId, prepareCt).ConfigureAwait(false);
                if (launched.ProgramId is { } programId &&
                    !await eligibility.CanReviewAsync(request.TenantId, programId, actor.MemberId, frozen.SystemInstanceId, prepareCt)
                        .ConfigureAwait(false))
                    return AccessReviewOutcome.Failure<FirstResponseDescriptor>(RequestErrorKind.NotFound,
                        "The review item was not found among the reviewer's current queue access.");
                SeparationOfDutiesWaiver? waiver = null;
                if (request.SeparationOfDutiesWaiverId is { } waiverId)
                    waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId, waiverId), prepareCt)
                        .ConfigureAwait(false);
                if (AccessReviewRules.CheckDecision(state, request.TenantId, request.CampaignId, request.Decision,
                        request.Rationale, actor.MemberId, actor.UserId, now, waiver) is { } failure)
                    return Result<FirstResponseDescriptor>.Failure(failure);
                var view = AccessReviewRules.BuildDecision(request.CommandId, state, request.Decision, request.Rationale,
                    actor.MemberId, actor.Reference, now, launched.Deadline, waiver?.Id);
                decisions = await AccessReviewContent.WriteAsync<IReadOnlyList<AccessDecisionView>>(content,
                    request.TenantId, AccessReviewContent.DecisionsSchema, [view], prepareCt).ConfigureAwait(false);
                intent = await AccessReviewContent.WriteAsync(content, request.TenantId,
                    AccessReviewContent.DecisionIntentSchema, request, prepareCt).ConfigureAwait(false);
                (response, result) = await AccessReviewContent.FreezeOkAsync(content, request.TenantId,
                    AccessReviewContent.DecisionViewSchema, view, prepareCt).ConfigureAwait(false);
                preparedCut = AccessReviewPreparedCut.Capture(owner);
                return Result<FirstResponseDescriptor>.Success(response!);
            },
            (AccessReviewItem owner, ImmutableContentRef frozenResponse) =>
            {
                if (preparedCut?.Matches(owner) != true)
                    return AccessReviewPreparedCut.Changed();
                return owner.RecordDecision(provenance,
                decisions!, intent!, frozenResponse, result!);
            }), context, ct).ConfigureAwait(false);
        return await AccessReviewContent.ResponseAsync<AccessDecisionView>(content, request.TenantId, outcome, ct)
            .ConfigureAwait(false);
    }
}
