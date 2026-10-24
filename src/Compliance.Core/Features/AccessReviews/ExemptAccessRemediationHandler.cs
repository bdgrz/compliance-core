using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Records the acting member's personal remediation exception approval as the root native fact of the item;
///     HTTP-only. The frozen first response is the complete exception.
/// </summary>
public sealed class ExemptAccessRemediationHandler(NativeOperationExecutor operations, AccessReviewReader campaigns,
    IImmutableContentStore content, TimeProvider clock, RestrictedApplicationVisibility visibility)
    : IRequestHandler<ExemptAccessRemediation, AccessRemediationExceptionView>
{
    const string CommandKind = nameof(ExemptAccessRemediation);

    public async ValueTask<Result<AccessRemediationExceptionView>> HandleAsync(
        IRequestContext<ExemptAccessRemediation> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return AccessReviewOutcome.Failure<AccessRemediationExceptionView>(RequestErrorKind.Forbidden,
                "Access review sign-off requires personal HTTP submission.");
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ExpectedRevision < 0)
            return AccessReviewOutcome.Failure<AccessRemediationExceptionView>(RequestErrorKind.Validation,
                "An exception requires a command id and a non-negative expected revision.");
        var actor = AccessReviewActor.From(context);
        var read = await campaigns.ReadItemAsync(request.TenantId, request.CampaignId, request.ItemId,
            request.CommandId, ct).ConfigureAwait(false);
        if (read.State is not { } state || read.Frozen is not { } frozen ||
            !await visibility.CanReadSystemInstanceAsync(request.TenantId, actor.UserId, frozen.SystemInstanceId, ct)
                .ConfigureAwait(false))
            return AccessReviewOutcome.Failure<AccessRemediationExceptionView>(RequestErrorKind.NotFound,
                "The review item was not found.");

        var now = clock.GetUtcNow();
        var provenance = AccessReviewItemWrites.Provenance(read, request.CommandId, CommandKind, actor.Reference,
            request.ExpectedRevision, AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? exception = null;
        ImmutableContentRef? result = null;
        if (read.Item.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            if (read.Item.PrepareChange(request.ExpectedRevision) is { IsSuccess: false } blocked)
                return Result<AccessRemediationExceptionView>.Failure(blocked.Error!);
            if (AccessReviewRules.CheckException(state, actor.MemberId, request.Rationale, request.ExpiresAt, now)
                is { } failure)
                return Result<AccessRemediationExceptionView>.Failure(failure);
            var view = new AccessRemediationExceptionView(request.CommandId, request.Rationale.Trim(),
                request.ExpiresAt, actor.Reference, now);
            exception = await AccessReviewContent.WriteAsync(content, request.TenantId,
                AccessReviewContent.ExceptionSchema, view, ct).ConfigureAwait(false);
            (response, result) = await AccessReviewContent.FreezeOkAsync(content, request.TenantId,
                AccessReviewContent.ExceptionViewSchema, view, ct).ConfigureAwait(false);
        }

        var observed = state.Revision;
        var outcome = await operations.ExecuteAsync(new NativeOperation<AccessReviewItem>(binding,
            () => new AccessReviewItem(request.TenantId, request.CampaignId, request.ItemId, request.CommandId),
            owner => owner.Acceptance,
            owner => AccessReviewItemWrites.Prepare(owner, request.ExpectedRevision, observed, response),
            (AccessReviewItem owner, ImmutableContentRef frozenResponse) => owner.RecordException(provenance,
                exception!, frozenResponse, result!)), context, ct).ConfigureAwait(false);
        return await AccessReviewContent.ResponseAsync<AccessRemediationExceptionView>(content, request.TenantId,
            outcome, ct).ConfigureAwait(false);
    }
}
