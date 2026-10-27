using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessReviews.Processes;
using Bdgrz.Compliance.Features.AccessReviews.Rules;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Features.AccessReviews.Handlers;

/// <summary>
///     The item pair of the generic acquire receiver for a bulk decision: it holds the exact head of the item that the
///     preview froze into the admitted intent. An item that changed since, that is closed, or that no longer carries the
///     frozen head rejects the batch and cancels it through the fences; an item already fenced against the batch is a
///     terminal no-op. While the hold is active the item refuses every other change, and a campaign completion waits for
///     it. The batch is never compared with the campaign revision.
/// </summary>
sealed class ReserveAccessReviewItemDecisionHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, AccessReviewBulkVerifier verifier,
    AccessReviewBulkFences fences)
    : AuthorizeSourceUseHandler<AccessReviewItem, ReserveAccessReviewItemDecision>(reader, executor, content, clock)
{
    readonly IAggregateReader _reader = reader;

    protected override AccessReviewItem Open(ReserveAccessReviewItemDecision request, Uuid selectedCommandId) =>
        new(Tenant(request.Target), request.CampaignId, request.SourceOwnerId, selectedCommandId, request.OperationId, request.SourceContentEventId);

    protected override ReservationKey ReceiptSelection(ReserveAccessReviewItemDecision request) =>
        AccessReviewEffects.BulkUseKey(request.OperationId, AccessReviewEffects.CampaignStream(Tenant(request.Target), request.CampaignId), request.Target);

    protected override async ValueTask<Result<SourceUseSource>?> VerifyReceiptSourceAsync(ReserveAccessReviewItemDecision request,
        AccessReviewItem owner, CancellationToken ct)
    {
        var tenant = Tenant(request.Target);
        if (request.Generation != AccessReviewEffects.Generation || request.SourceOwnerId != owner.ItemId || request.DecisionBatchId != request.OperationId)
            return Result<SourceUseSource>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The request differs from its selected Access source-use owner or generation.", false));
        var native = await verifier.ReadNativeAsync(tenant, request.CampaignId, request.Source, request.Intent,
            request.OperationId, ct).ConfigureAwait(false);
        if (native.Error is not null)
            return Result<SourceUseSource>.Failure(native.Error);
        var campaign = native.Campaign!;
        var parent = native.Admitted!.Operation;
        var key = ReceiptSelection(request)!;
        var recorded = await AccessReviewSourceUseReceipts.ReadAsync(Content, owner, key, parent, request.Source,
            request.Intent, SourceUsePurpose.BulkDecision, false, false, ct).ConfigureAwait(false);
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
        var batch = await _reader.HydrateAsync(new AccessReviewDecisionBatch(tenant, request.CampaignId,
            request.DecisionBatchId), ct).ConfigureAwait(false);
        var superseded = batch.CancellationAccepted is not null ? "The bulk decision was canceled."
            : batch.Published is not null ? "The bulk decision already published." : null;
        return Result<SourceUseSource>.Success(new SourceUseSource(intent.Header, intent.ParentIntent, key,
            intent.Requirement, nameof(ReserveAccessReviewItemDecision), null, superseded));
    }

    protected override async ValueTask<Result<SourceUseSource>> VerifyAsync(ReserveAccessReviewItemDecision request,
        CancellationToken ct)
    {
        var tenant = Tenant(request.Target);
        var bulk = await verifier.VerifyAsync(tenant, request.CampaignId, request.Source, request.Intent,
            request.OperationId, ct).ConfigureAwait(false);
        if (bulk.Error is not null)
            return Result<SourceUseSource>.Failure(bulk.Error);
        var (campaign, intent) = (bulk.Campaign!, bulk.Intent!);
        var selected = await verifier.ItemOfAsync(request.Target.Realm, intent, request.Target.Resource, ct)
            .ConfigureAwait(false);
        if (selected is null || selected.ItemId != request.SourceOwnerId ||
            request.DecisionBatchId != request.OperationId || request.Generation != AccessReviewEffects.Generation)
            return Result<SourceUseSource>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The hold differs from the frozen bulk decision selection.", false));
        NativeFactReference head;
        try
        {
            head = await verifier.HeadOfAsync(tenant, selected, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<SourceUseSource>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The frozen item head cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
        }

        if (head.EventId != request.SourceContentEventId || selected.ExpectedItemRevision != request.ExpectedContentRevision)
            return Result<SourceUseSource>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The hold differs from the frozen item head.", false));
        var requirement = new SourceUseRequirement(request.Target, head, selected.ExpectedItemRevision,
            SourceUsePurpose.BulkDecision, selected.EligibilityProofs.Content);
        var batch = await _reader.HydrateAsync(new AccessReviewDecisionBatch(tenant, request.CampaignId,
            request.DecisionBatchId), ct).ConfigureAwait(false);
        var superseded = batch.CancellationAccepted is not null ? "The bulk decision was canceled."
            : batch.Published is not null ? "The bulk decision already published."
            : null;
        return Result<SourceUseSource>.Success(new SourceUseSource(intent.Header, request.Intent,
            AccessReviewEffects.BulkUseKey(request.OperationId, campaign.Stream, request.Target), requirement,
            nameof(ReserveAccessReviewItemDecision), null, superseded));
    }

    protected override ValueTask<Result> VerifyPinnedAsync(AccessReviewItem owner, SourceUseRequirement requirement,
        CancellationToken ct)
    {
        if (owner.Opened is null)
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The review item is not materialized yet.", true)));
        if (owner.IsClosed)
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "A completed campaign cannot change.", false)));
        if (!IsPinned(owner, requirement))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The item changed after the bulk decision was previewed.", false)));
        return ValueTask.FromResult(owner.Uses.HasActiveUse
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Another operation holds this item; retry after it settles.", true))
            : Result.Success);
    }

    protected override bool IsPinned(AccessReviewItem owner, SourceUseRequirement requirement) =>
        !owner.IsClosed && owner.Latest is { } latest && latest.Metadata!.EventId == requirement.ContentEvent.EventId &&
        (ulong)owner.Revision == requirement.ResourceRevision;

    protected override string? FencedReason(AccessReviewItem owner, ReservationKey key) =>
        owner.IsFenced(key.RootOperationId) ? "The item was fenced against the canceled bulk decision." : null;

    protected override Func<AccessReviewItem, OperationProvenance, ImmutableContentRef, long, Result> GrantOperation() =>
        (AccessReviewItem item, OperationProvenance provenance, ImmutableContentRef intent, long generation) =>
            item.AuthorizeBulkDecision(provenance, intent, generation);

    protected override async ValueTask<Result<ReceiverOutcome>> RefuseAsync(ReserveAccessReviewItemDecision request,
        SourceUseSource source, string reason, IExecutionContext context, CancellationToken ct)
    {
        var bulk = await verifier.VerifyAsync(Tenant(request.Target), request.CampaignId, request.Source,
            request.Intent, request.OperationId, ct).ConfigureAwait(false);
        return bulk.Error is not null
            ? Result<ReceiverOutcome>.Failure(bulk.Error)
            : await fences.CancelAsync(bulk, request.Source, reason, request.SourceOwnerId, context, ct)
                .ConfigureAwait(false);
    }

    static Uuid Tenant(EventStreamAddress stream) => Uuid.Parse(stream.Realm, CultureInfo.InvariantCulture);
}
