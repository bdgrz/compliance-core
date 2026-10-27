using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AccessReviews.Readers;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews.Handlers;

/// <summary>
///     Replaces an ineligible frozen campaign owner with an attributable root native fact of the item. The frozen
///     first response is the complete reassignment, so an exact retry returns it even after the item changed again.
/// </summary>
public sealed class ReassignAccessReviewResponsibilityHandler(NativeOperationExecutor operations,
    AccessReviewReader campaigns, IImmutableContentStore content, TimeProvider clock,
    AccessReviewQueueEligibility eligibility, OperatingAuthority authority, RestrictedApplicationVisibility visibility)
    : IRequestHandler<ReassignAccessReviewResponsibility, AccessReviewResponsibilityReassignmentView>
{
    const string CommandKind = nameof(ReassignAccessReviewResponsibility);

    public async ValueTask<Result<AccessReviewResponsibilityReassignmentView>> HandleAsync(
        IRequestContext<ReassignAccessReviewResponsibility> context, CancellationToken ct)
    {
        try
        {
            return await HandleCoreAsync(context, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<AccessReviewResponsibilityReassignmentView>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The exact access item source cannot be verified: {error.Message}", error is ImmutableContentUnavailableException));
        }
    }

    async ValueTask<Result<AccessReviewResponsibilityReassignmentView>> HandleCoreAsync(
        IRequestContext<ReassignAccessReviewResponsibility> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ExpectedRevision < 0)
            return Failure(RequestErrorKind.Validation,
                "A reassignment requires a command id and a non-negative expected revision.");
        var actor = AccessReviewActor.From(context);
        var read = await campaigns.ReadItemIdentityAsync(request.TenantId, request.CampaignId, request.ItemId,
            request.CommandId, ct).ConfigureAwait(false);
        if (read.Campaign.Launched is not { ProgramId: { } programId, RemediationOwnerMemberId: not null } ||
            read.Frozen is not { } item ||
            !await visibility.CanReadSystemInstanceAsync(request.TenantId, actor.UserId, item.SystemInstanceId, ct)
                .ConfigureAwait(false))
            return Failure(RequestErrorKind.NotFound, "The review item was not found.");
        if (!await authority.HasProgramManagementPermissionAsync(request.TenantId, programId, actor.MemberId, ct)
                .ConfigureAwait(false))
            return Failure(RequestErrorKind.Forbidden, "The actor must currently manage the campaign's owner Program.");

        var now = clock.GetUtcNow();
        var provenance = AccessReviewItemWrites.Provenance(read, request.CommandId, CommandKind, actor.Reference,
            request.ExpectedRevision, AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);
        FirstResponseDescriptor? response = null;
        AccessReviewPreparedCut? preparedCut = null;
        ImmutableContentRef? reassignment = null;
        ImmutableContentRef? result = null;
        var accepted = await operations.ExecuteAsync(new NativeOperation<AccessReviewItem>(binding,
            () => new AccessReviewItem(request.TenantId, request.CampaignId, request.ItemId, request.CommandId),
            owner => owner.Acceptance,
            async (owner, prepareCt) =>
            {
                var prepared = owner.PrepareChange(request.ExpectedRevision);
                if (!prepared.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
                var state = await campaigns.ReadStateAsync(request.TenantId, owner, item,
                    read.Campaign.Launched?.RemediationOwnerMemberId, prepareCt).ConfigureAwait(false);
                if (!AccessReviewResponsibilityKind.IsKnown(request.Responsibility) ||
                    request.AssignedMemberId == Uuid.Empty || !AccessReviewVocabulary.IsBoundedText(request.Reason, 2000))
                    return AccessReviewOutcome.Failure<FirstResponseDescriptor>(RequestErrorKind.Validation,
                        "A reassignment requires a known responsibility, member, and reason.");
                if (request.Responsibility == AccessReviewResponsibilityKind.Reviewer &&
                    !AccessReviewVocabulary.IsBoundedText(request.DelegationReason, 2000))
                    return AccessReviewOutcome.Failure<FirstResponseDescriptor>(RequestErrorKind.Validation,
                        "A reviewer replacement is a delegate and requires a delegation reason.");
                if (request.Responsibility == AccessReviewResponsibilityKind.RemediationOwner &&
                    request.DelegationReason is not null)
                    return AccessReviewOutcome.Failure<FirstResponseDescriptor>(RequestErrorKind.Validation,
                        "A remediation-owner reassignment cannot include a reviewer delegation reason.");
                var previous = request.Responsibility == AccessReviewResponsibilityKind.Reviewer
                    ? state.CurrentReviewerMemberId
                    : state.CurrentRemediationOwnerMemberId;
                if (previous is not { } previousMemberId || previousMemberId == Uuid.Empty)
                    return AccessReviewOutcome.Failure<FirstResponseDescriptor>(RequestErrorKind.NotFound, "The responsibility was not found.");
                var previousEligible = request.Responsibility == AccessReviewResponsibilityKind.Reviewer
                    ? await eligibility.CanReviewAsync(request.TenantId, programId, previousMemberId,
                        item.SystemInstanceId, prepareCt).ConfigureAwait(false)
                    : await eligibility.CanRemediateAsync(request.TenantId, programId, previousMemberId,
                        item.SystemInstanceId, prepareCt).ConfigureAwait(false);
                if (previousEligible)
                    return AccessReviewOutcome.Failure<FirstResponseDescriptor>(RequestErrorKind.Conflict,
                        "A responsibility can be reassigned only after its current owner loses eligibility.");
                var distinctMemberId = request.Responsibility == AccessReviewResponsibilityKind.Reviewer
                    ? state.CurrentRemediationOwnerMemberId
                    : state.CurrentReviewerMemberId;
                if (request.AssignedMemberId == distinctMemberId)
                    return AccessReviewOutcome.Failure<FirstResponseDescriptor>(RequestErrorKind.Validation, "The reviewer and remediation owner must remain distinct.");
                var assignedEligible = request.Responsibility == AccessReviewResponsibilityKind.Reviewer
                    ? await eligibility.CanReviewAsync(request.TenantId, programId, request.AssignedMemberId,
                        item.SystemInstanceId, prepareCt).ConfigureAwait(false)
                    : await eligibility.CanRemediateAsync(request.TenantId, programId, request.AssignedMemberId,
                        item.SystemInstanceId, prepareCt).ConfigureAwait(false);
                if (!assignedEligible)
                    return AccessReviewOutcome.Failure<FirstResponseDescriptor>(RequestErrorKind.Validation,
                        "The replacement must have current queue-read, access-review, membership, and restricted-system eligibility for this responsibility.");
                if (AccessReviewRules.CheckReassignment(state, request.Responsibility, request.AssignedMemberId,
                        request.Reason, request.DelegationReason, actor.Reference, now, now) is { } failure)
                    return Result<FirstResponseDescriptor>.Failure(failure);
                var view = new AccessReviewResponsibilityReassignmentView(request.CommandId, request.Responsibility,
                    request.ItemId, previousMemberId, request.AssignedMemberId, request.Reason.Trim(), actor.Reference, now,
                    request.DelegationReason?.Trim());
                reassignment = await AccessReviewContent.WriteAsync(content, request.TenantId,
                    AccessReviewContent.ReassignmentSchema, view, prepareCt).ConfigureAwait(false);
                (response, result) = await AccessReviewContent.FreezeOkAsync(content, request.TenantId,
                    AccessReviewContent.ReassignmentViewSchema, view, prepareCt).ConfigureAwait(false);
                preparedCut = AccessReviewPreparedCut.Capture(owner);
                return Result<FirstResponseDescriptor>.Success(response!);
            },
            (AccessReviewItem owner, ImmutableContentRef frozenResponse) =>
            {
                if (preparedCut?.Matches(owner) != true)
                    return AccessReviewPreparedCut.Changed();
                return owner.Reassign(provenance,
                reassignment!, frozenResponse, result!);
            }), context, ct).ConfigureAwait(false);
        return await AccessReviewContent.ResponseAsync<AccessReviewResponsibilityReassignmentView>(content,
            request.TenantId, accepted, ct).ConfigureAwait(false);
    }

    static Result<AccessReviewResponsibilityReassignmentView> Failure(RequestErrorKind kind, string message) =>
        AccessReviewOutcome.Failure<AccessReviewResponsibilityReassignmentView>(kind, message);
}
