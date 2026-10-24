using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     The item pair of the generic acquire receiver: it holds the exact head of the item that the completion pinned
///     at admission. An item that changed since, or that no longer satisfies the completion rules, rejects the
///     completion and fences it. While the use is held the item refuses every change.
/// </summary>
sealed class AuthorizeAccessReviewItemCampaignCompletionUseHandler(IAggregateReader reader,
    IAggregateExecutor executor, IImmutableContentStore content, TimeProvider clock, TimeProvider time,
    AccessReviewCompletionVerifier verifier, AccessReviewCompletionFences fences)
    : AuthorizeSourceUseHandler<AccessReviewItem, AuthorizeAccessReviewItemCampaignCompletionUse>(reader, executor,
        content, clock)
{
    protected override AccessReviewItem Open(AuthorizeAccessReviewItemCampaignCompletionUse request,
        Uuid selectedCommandId) => new(Tenant(request.Target), request.CampaignId, request.SourceOwnerId, selectedCommandId);

    protected override async ValueTask<Result<SourceUseSource>> VerifyAsync(
        AuthorizeAccessReviewItemCampaignCompletionUse request, CancellationToken ct)
    {
        var tenant = Tenant(request.Target);
        var completion = await verifier.VerifyAsync(tenant, request.CampaignId, request.Source, request.Intent,
            request.OperationId, Uuid.Empty, ct).ConfigureAwait(false);
        if (completion.Error is not null)
            return Result<SourceUseSource>.Failure(completion.Error);
        var (campaign, intent) = (completion.Campaign!, completion.Intent!);
        var requirement = await verifier.RequirementOfAsync(request.Target.Realm, intent, request.Target.Resource, ct)
            .ConfigureAwait(false);
        if (requirement is null ||
            request.Generation != AccessReviewEffects.Generation ||
            requirement.ContentEvent.EventId != request.SourceContentEventId ||
            requirement.ResourceRevision != request.ExpectedContentRevision)
            return Result<SourceUseSource>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The authorization differs from the frozen item use requirement.", false));
        var superseded = campaign.CanceledFor(request.OperationId) is not null ? "The completion was canceled."
            : campaign.ClosedFor(request.OperationId) is not null ? "The completion already closed the campaign."
            : null;
        return Result<SourceUseSource>.Success(new SourceUseSource(intent.Header, request.Intent,
            AccessReviewCompletionVerifier.UseKey(request.OperationId, campaign, requirement), requirement,
            nameof(AuthorizeAccessReviewItemCampaignCompletionUse), null, superseded));
    }

    protected override async ValueTask<Result> VerifyPinnedAsync(AccessReviewItem owner,
        SourceUseRequirement requirement, CancellationToken ct)
    {
        if (owner.Opened is null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The review item is not materialized yet.", true));
        if (!IsPinned(owner, requirement))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The item changed after the completion was admitted.", false));
        try
        {
            var frozen = await AccessReviewItemFold.FrozenItemAsync(owner, Content, Tenant(owner.Stream), ct)
                .ConfigureAwait(false);
            var state = await AccessReviewItemFold.ReadAsync(owner, frozen, null, Content, Tenant(owner.Stream), ct)
                .ConfigureAwait(false);
            return state.CompletionBlocker(time.GetUtcNow()) is { } blocker
                ? Result.Failure(new RequestError(RequestErrorKind.Conflict, blocker, false))
                : Result.Success;
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The pinned item content cannot be verified: {exception.Message}", false));
        }
    }

    protected override bool IsPinned(AccessReviewItem owner, SourceUseRequirement requirement) =>
        owner.Latest is { } latest && latest.Metadata!.EventId == requirement.ContentEvent.EventId &&
        (ulong)owner.Revision == requirement.ResourceRevision;

    protected override Func<AccessReviewItem, OperationProvenance, ImmutableContentRef, long, Result> GrantOperation() =>
        (AccessReviewItem item, OperationProvenance provenance, ImmutableContentRef intent, long generation) =>
            item.AuthorizeCampaignCompletionUse(provenance, intent, generation);

    protected override async ValueTask<Result<ReceiverOutcome>> RefuseAsync(
        AuthorizeAccessReviewItemCampaignCompletionUse request, SourceUseSource source, string reason,
        IExecutionContext context, CancellationToken ct)
    {
        var completion = await verifier.VerifyAsync(Tenant(request.Target), request.CampaignId, request.Source,
            request.Intent, request.OperationId, Uuid.Empty, ct).ConfigureAwait(false);
        return completion.Error is not null
            ? Result<ReceiverOutcome>.Failure(completion.Error)
            : await fences.AbortAsync(completion, request.Source, reason, request.SourceOwnerId, context, ct)
                .ConfigureAwait(false);
    }

    static Uuid Tenant(EventStreamAddress stream) => Uuid.Parse(stream.Realm, CultureInfo.InvariantCulture);
}
