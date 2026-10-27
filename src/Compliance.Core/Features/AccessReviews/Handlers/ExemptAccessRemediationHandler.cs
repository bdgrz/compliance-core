using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AccessReviews.Readers;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews.Handlers;

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
        try
        {
            return await HandleCoreAsync(context, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<AccessRemediationExceptionView>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The exact access item source cannot be verified: {error.Message}", error is ImmutableContentUnavailableException));
        }
    }

    async ValueTask<Result<AccessRemediationExceptionView>> HandleCoreAsync(
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
        var read = await campaigns.ReadItemIdentityAsync(request.TenantId, request.CampaignId, request.ItemId,
            request.CommandId, ct).ConfigureAwait(false);
        if (read.Frozen is not { } frozen ||
            !await visibility.CanReadSystemInstanceAsync(request.TenantId, actor.UserId, frozen.SystemInstanceId, ct)
                .ConfigureAwait(false))
            return AccessReviewOutcome.Failure<AccessRemediationExceptionView>(RequestErrorKind.NotFound,
                "The review item was not found.");

        var now = clock.GetUtcNow();
        var provenance = AccessReviewItemWrites.Provenance(read, request.CommandId, CommandKind, actor.Reference,
            request.ExpectedRevision, AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);
        FirstResponseDescriptor? response = null;
        AccessReviewPreparedCut? preparedCut = null;
        ImmutableContentRef? exception = null;
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
                if (AccessReviewRules.CheckException(state, actor.MemberId, request.Rationale, request.ExpiresAt, now)
                    is { } failure)
                    return Result<FirstResponseDescriptor>.Failure(failure);
                var view = new AccessRemediationExceptionView(request.CommandId, request.Rationale.Trim(),
                    request.ExpiresAt, actor.Reference, now);
                exception = await AccessReviewContent.WriteAsync(content, request.TenantId,
                    AccessReviewContent.ExceptionSchema, view, prepareCt).ConfigureAwait(false);
                (response, result) = await AccessReviewContent.FreezeOkAsync(content, request.TenantId,
                    AccessReviewContent.ExceptionViewSchema, view, prepareCt).ConfigureAwait(false);
                preparedCut = AccessReviewPreparedCut.Capture(owner);
                return Result<FirstResponseDescriptor>.Success(response!);
            },
            (AccessReviewItem owner, ImmutableContentRef frozenResponse) =>
            {
                if (preparedCut?.Matches(owner) != true)
                    return AccessReviewPreparedCut.Changed();
                return owner.RecordException(provenance,
                exception!, frozenResponse, result!);
            }), context, ct).ConfigureAwait(false);
        return await AccessReviewContent.ResponseAsync<AccessRemediationExceptionView>(content, request.TenantId,
            outcome, ct).ConfigureAwait(false);
    }
}
