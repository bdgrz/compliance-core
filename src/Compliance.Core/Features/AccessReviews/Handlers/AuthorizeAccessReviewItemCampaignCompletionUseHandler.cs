using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessReviews.History;
using Bdgrz.Compliance.Features.AccessReviews.Processes;
using Bdgrz.Compliance.Features.AccessReviews.Rules;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
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
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SourceUse.Services;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Features.AccessReviews.Handlers;

/// <summary>
///     The item pair of the generic acquire receiver: it holds the exact head of the item that the completion pinned
///     at admission. An item that changed since, or that no longer satisfies the completion rules, rejects the
///     completion and fences it. An item that an admitted bulk decision batch selects is held only after that batch
///     settled on it (a wait, never a failure), and then at the head the batch left it at. While the use is held the
///     item refuses every change.
/// </summary>
sealed class AuthorizeAccessReviewItemCampaignCompletionUseHandler(IAggregateReader reader,
    IAggregateExecutor executor, IImmutableContentStore content, TimeProvider clock, TimeProvider time,
    AccessReviewCompletionVerifier verifier, AccessReviewCompletionFences fences, AccessReviewManifests manifests, IDomainEventReader events)
    : AuthorizeSourceUseHandler<AccessReviewItem, AuthorizeAccessReviewItemCampaignCompletionUse>(reader, executor,
        content, clock)
{
    readonly IAggregateReader _reader = reader;

    protected override AccessReviewItem Open(AuthorizeAccessReviewItemCampaignCompletionUse request,
        Uuid selectedCommandId) => new(Tenant(request.Target), request.CampaignId, request.SourceOwnerId, selectedCommandId, selectedContentEventId: request.SourceContentEventId);

    protected override ReservationKey ReceiptSelection(AuthorizeAccessReviewItemCampaignCompletionUse request) =>
        new(request.OperationId, AccessReviewEffects.CampaignStream(Tenant(request.Target), request.CampaignId), (ulong)AccessReviewEffects.Generation, request.Target, SourceUseReservations.PurposeName(SourceUsePurpose.CampaignCompletion));

    protected override async ValueTask<Result<SourceUseSource>?> VerifyReceiptSourceAsync(AuthorizeAccessReviewItemCampaignCompletionUse request,
        AccessReviewItem owner, CancellationToken ct)
    {
        var tenant = Tenant(request.Target);
        if (request.Generation != AccessReviewEffects.Generation || request.SourceOwnerId != owner.ItemId)
            return Result<SourceUseSource>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The request differs from its selected Access source-use owner or generation.", false));
        var native = await verifier.ReadNativeAsync(tenant, request.CampaignId, request.Source, request.Intent,
            request.OperationId, Uuid.Empty, ct).ConfigureAwait(false);
        if (native.Error is not null)
            return Result<SourceUseSource>.Failure(native.Error);
        var campaign = native.Campaign!;
        var parent = native.Accepted!.Operation;
        var key = ReceiptSelection(request)!;
        var recorded = await AccessReviewSourceUseReceipts.ReadAsync(Content, owner, key, parent, request.Source,
            request.Intent, SourceUsePurpose.CampaignCompletion, false, true, ct).ConfigureAwait(false);
        if (recorded is null)
            return null;
        var result = recorded.Value;
        if (!result.IsSuccess)
            return Result<SourceUseSource>.Failure(result.Error!);
        var intent = result.Value!;
        if (intent.Requirement.ContentEvent.EventId != request.SourceContentEventId ||
            intent.Requirement.ResourceRevision != request.ExpectedContentRevision)
            return Result<SourceUseSource>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The authorization differs from its exact recorded source use requirement.", false));
        var superseded = campaign.CanceledFor(request.OperationId) is not null ? "The completion was canceled."
            : campaign.ClosedFor(request.OperationId) is not null ? "The completion already closed the campaign." : null;
        return Result<SourceUseSource>.Success(new SourceUseSource(intent.Header, intent.ParentIntent, key,
            intent.Requirement, nameof(AuthorizeAccessReviewItemCampaignCompletionUse), null, superseded));
    }

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
        var drained = await DrainAsync(request, intent, ct).ConfigureAwait(false);
        if (drained is not null)
            return Result<SourceUseSource>.Failure(drained);
        var superseded = campaign.CanceledFor(request.OperationId) is not null ? "The completion was canceled."
            : campaign.ClosedFor(request.OperationId) is not null ? "The completion already closed the campaign."
            : null;
        return Result<SourceUseSource>.Success(new SourceUseSource(intent.Header, request.Intent,
            AccessReviewCompletionVerifier.UseKey(request.OperationId, campaign, requirement), requirement,
            nameof(AuthorizeAccessReviewItemCampaignCompletionUse), null, superseded));
    }

    /// <summary>
    ///     A completion drains the bulk decision batches admitted before its seal: the item is held only once every one of
    ///     them that selects it has settled (a settlement before any hold counts, it is the fence). Waiting is transient.
    /// </summary>
    async ValueTask<RequestError?> DrainAsync(AuthorizeAccessReviewItemCampaignCompletionUse request,
        ClosureIntent intent, CancellationToken ct)
    {
        IReadOnlyDictionary<Uuid, IReadOnlyList<AccessReviewAdmittedBatchSelection>> drains;
        try
        {
            drains = await manifests.DrainsAsync(request.Target.Realm, intent.AdmittedChildren, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return new RequestError(RequestErrorKind.Conflict,
                $"The admitted bulk decisions cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException);
        }

        if (!drains.TryGetValue(request.SourceOwnerId, out var selections))
            return null;
        var selected = Open(request, Uuid.Empty);
        selected.ForSourceUses(selections.Select(selection => selected.BulkKey(selection.DecisionBatchId)).ToArray());
        var owner = await _reader.HydrateAsync(selected, ct).ConfigureAwait(false);
        foreach (var selection in selections)
            if (owner.Uses.Find(owner.BulkKey(selection.DecisionBatchId)) is not { IsSettled: true })
                return new RequestError(RequestErrorKind.Conflict,
                    $"The admitted bulk decision {selection.DecisionBatchId} has not drained from item {request.SourceOwnerId} yet.",
                    true);
        return null;
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
        if (owner.Uses.HasActiveUse)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Another operation holds this item; retry after it settles.", true));
        try
        {
            var frozen = await AccessReviewItemFold.FrozenItemAsync(owner, Content, Tenant(owner.Stream), ct)
                .ConfigureAwait(false);
            var history = await AccessReviewItemHistory.ReadAsync(events, owner, ct).ConfigureAwait(false);
            var state = await AccessReviewItemFold.ReadAsync(owner, history, frozen, null, Content, Tenant(owner.Stream), ct)
                .ConfigureAwait(false);
            return state.CompletionBlocker(time.GetUtcNow()) is { } blocker
                ? Result.Failure(new RequestError(RequestErrorKind.Conflict, blocker, false))
                : Result.Success;
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The pinned item content cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
        }
    }

    protected override bool IsPinned(AccessReviewItem owner, SourceUseRequirement requirement) =>
        owner.MatchesPin(requirement.ContentEvent.EventId, requirement.ResourceRevision);

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
