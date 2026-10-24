using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

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
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ExpectedRevision < 0)
            return Failure(RequestErrorKind.Validation,
                "A reassignment requires a command id and a non-negative expected revision.");
        var actor = AccessReviewActor.From(context);
        var read = await campaigns.ReadItemAsync(request.TenantId, request.CampaignId, request.ItemId,
            request.CommandId, ct).ConfigureAwait(false);
        if (read.Campaign.Launched is not { ProgramId: { } programId, RemediationOwnerMemberId: not null } ||
            read.State is not { } state || read.Frozen is not { } item ||
            !await visibility.CanReadSystemInstanceAsync(request.TenantId, actor.UserId, item.SystemInstanceId, ct)
                .ConfigureAwait(false))
            return Failure(RequestErrorKind.NotFound, "The review item was not found.");
        if (!await authority.HasProgramManagementPermissionAsync(request.TenantId, programId, actor.MemberId, ct)
                .ConfigureAwait(false))
            return Failure(RequestErrorKind.Forbidden, "The actor must currently manage the campaign's owner Program.");

        if (!AccessReviewResponsibilityKind.IsKnown(request.Responsibility) ||
            request.AssignedMemberId == Uuid.Empty || !AccessReviewVocabulary.IsBoundedText(request.Reason, 2000))
            return Failure(RequestErrorKind.Validation,
                "A reassignment requires a known responsibility, member, and reason.");
        if (request.Responsibility == AccessReviewResponsibilityKind.Reviewer &&
            !AccessReviewVocabulary.IsBoundedText(request.DelegationReason, 2000))
            return Failure(RequestErrorKind.Validation,
                "A reviewer replacement is a delegate and requires a delegation reason.");
        if (request.Responsibility == AccessReviewResponsibilityKind.RemediationOwner &&
            request.DelegationReason is not null)
            return Failure(RequestErrorKind.Validation,
                "A remediation-owner reassignment cannot include a reviewer delegation reason.");

        var now = clock.GetUtcNow();
        var provenance = AccessReviewItemWrites.Provenance(read, request.CommandId, CommandKind, actor.Reference,
            request.ExpectedRevision, AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? reassignment = null;
        ImmutableContentRef? result = null;
        if (read.Item.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            if (read.Item.PrepareChange(request.ExpectedRevision) is { IsSuccess: false } blocked)
                return Result<AccessReviewResponsibilityReassignmentView>.Failure(blocked.Error!);
            var previous = request.Responsibility == AccessReviewResponsibilityKind.Reviewer
                ? state.CurrentReviewerMemberId
                : state.CurrentRemediationOwnerMemberId;
            if (previous is not { } previousMemberId || previousMemberId == Uuid.Empty)
                return Failure(RequestErrorKind.NotFound, "The responsibility was not found.");
            var previousEligible = request.Responsibility == AccessReviewResponsibilityKind.Reviewer
                ? await eligibility.CanReviewAsync(request.TenantId, programId, previousMemberId,
                    item.SystemInstanceId, ct).ConfigureAwait(false)
                : await eligibility.CanRemediateAsync(request.TenantId, programId, previousMemberId,
                    item.SystemInstanceId, ct).ConfigureAwait(false);
            if (previousEligible)
                return Failure(RequestErrorKind.Conflict,
                    "A responsibility can be reassigned only after its current owner loses eligibility.");
            var distinctMemberId = request.Responsibility == AccessReviewResponsibilityKind.Reviewer
                ? state.CurrentRemediationOwnerMemberId
                : state.CurrentReviewerMemberId;
            if (request.AssignedMemberId == distinctMemberId)
                return Failure(RequestErrorKind.Validation, "The reviewer and remediation owner must remain distinct.");
            var assignedEligible = request.Responsibility == AccessReviewResponsibilityKind.Reviewer
                ? await eligibility.CanReviewAsync(request.TenantId, programId, request.AssignedMemberId,
                    item.SystemInstanceId, ct).ConfigureAwait(false)
                : await eligibility.CanRemediateAsync(request.TenantId, programId, request.AssignedMemberId,
                    item.SystemInstanceId, ct).ConfigureAwait(false);
            if (!assignedEligible)
                return Failure(RequestErrorKind.Validation,
                    "The replacement must have current queue-read, access-review, membership, and restricted-system eligibility for this responsibility.");
            if (AccessReviewRules.CheckReassignment(state, request.Responsibility, request.AssignedMemberId,
                    request.Reason, request.DelegationReason, actor.Reference, now, now) is { } failure)
                return Result<AccessReviewResponsibilityReassignmentView>.Failure(failure);
            var view = new AccessReviewResponsibilityReassignmentView(request.CommandId, request.Responsibility,
                request.ItemId, previousMemberId, request.AssignedMemberId, request.Reason.Trim(), actor.Reference, now,
                request.DelegationReason?.Trim());
            reassignment = await AccessReviewContent.WriteAsync(content, request.TenantId,
                AccessReviewContent.ReassignmentSchema, view, ct).ConfigureAwait(false);
            (response, result) = await AccessReviewContent.FreezeOkAsync(content, request.TenantId,
                AccessReviewContent.ReassignmentViewSchema, view, ct).ConfigureAwait(false);
        }

        var observed = state.Revision;
        var accepted = await operations.ExecuteAsync(new NativeOperation<AccessReviewItem>(binding,
            () => new AccessReviewItem(request.TenantId, request.CampaignId, request.ItemId, request.CommandId),
            owner => owner.Acceptance,
            owner => AccessReviewItemWrites.Prepare(owner, request.ExpectedRevision, observed, response),
            (AccessReviewItem owner, ImmutableContentRef frozenResponse) => owner.Reassign(provenance,
                reassignment!, frozenResponse, result!)), context, ct).ConfigureAwait(false);
        return await AccessReviewContent.ResponseAsync<AccessReviewResponsibilityReassignmentView>(content,
            request.TenantId, accepted, ct).ConfigureAwait(false);
    }

    static Result<AccessReviewResponsibilityReassignmentView> Failure(RequestErrorKind kind, string message) =>
        AccessReviewOutcome.Failure<AccessReviewResponsibilityReassignmentView>(kind, message);
}
