using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews.Aggregates;

/// <summary>
///     One bulk decision batch of one campaign: its immutable preview and item selection (the admitted
///     <see cref="BulkAccessDecisionIntent" />, kept as verified content), the shared-rationale decision and the one
///     all-item publication decision. The identity derives from the tenant, campaign and batch ids. The logical
///     revision is its own: the materialization is revision zero, a publication or a cancellation acceptance is
///     revision one and the completed cancellation is revision two. It is never compared with the campaign revision
///     that admitted the batch. Item holds, provisional decisions and fences live on the item streams; the batch owns
///     only the decision to publish all of them or to cancel and fence them all.
/// </summary>
public sealed class AccessReviewDecisionBatch : Aggregate
{
    /// <summary>Stream area of every decision batch (the realm is the tenant).</summary>
    public const string Area = "access-review-decision-batches";

    readonly Uuid _tenantId;
    readonly Uuid _campaignId;
    readonly Uuid _decisionBatchId;
    readonly List<DomainEvent> _facts = [];
    AccessReviewDecisionBatchAccepted? _accepted;
    AccessReviewDecisionBatchPublished? _published;
    AccessReviewDecisionBatchCancellationAccepted? _cancellation;
    AccessReviewDecisionBatchCanceled? _canceled;
    long _revision;

    /// <summary>Opens the batch, selecting the logical operation to resolve.</summary>
    public AccessReviewDecisionBatch(Uuid tenantId, Uuid campaignId, Uuid decisionBatchId, Uuid selectedLogicalId = default)
        : base(AccessReviewEffects.BatchStreamId(tenantId, campaignId, decisionBatchId),
            AccessReviewEffects.BatchStream(tenantId, campaignId, decisionBatchId))
    {
        (_tenantId, _campaignId, _decisionBatchId) = (tenantId, campaignId, decisionBatchId);
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<AccessReviewDecisionBatchAccepted>(ev =>
        {
            Acceptance.Observe(ev);
            if (_accepted is not null || ev.Owner != Stream || ev.Operation.Owner != Stream || ev.Revision != 0 ||
                ev.Operation.RootOperationId != _decisionBatchId)
                throw new InvalidOperationException(
                    "A batch is materialized once, at revision zero, on the stream its tenant, campaign and batch derive.");
            _accepted = ev;
            _revision = 0;
            _facts.Add(ev);
        });
        On<AccessReviewDecisionBatchPublished>(ev =>
        {
            Acceptance.Observe(ev);
            if (_accepted is null || _published is not null || _cancellation is not null || ev.Owner != Stream ||
                ev.Operation.Owner != Stream || ev.Revision != _revision + 1 || ev.Intent != _accepted.Intent ||
                ev.SourceProofs is null || ev.OperationResult is null)
                throw new InvalidOperationException("A materialized batch publishes once, and never after a cancellation.");
            _published = ev;
            _revision = ev.Revision;
            _facts.Add(ev);
        });
        On<AccessReviewDecisionBatchCancellationAccepted>(ev =>
        {
            Acceptance.Observe(ev);
            if (_accepted is null || _published is not null || _cancellation is not null || ev.Owner != Stream ||
                ev.Operation.Owner != Stream || ev.Revision != _revision + 1 || ev.Intent != _accepted.Intent)
                throw new InvalidOperationException(
                    "A materialized, unpublished batch accepts one cancellation.");
            _cancellation = ev;
            _revision = ev.Revision;
            _facts.Add(ev);
        });
        On<AccessReviewDecisionBatchCanceled>(ev =>
        {
            Acceptance.Observe(ev);
            if (_cancellation is null || _canceled is not null || ev.Owner != Stream || ev.Operation.Owner != Stream ||
                ev.Revision != _revision + 1 || ev.Intent != _accepted!.Intent)
                throw new InvalidOperationException("Only an accepted cancellation completes.");
            _canceled = ev;
            _revision = ev.Revision;
            _facts.Add(ev);
        });
    }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The campaign this batch belongs to.</summary>
    public Uuid CampaignId => _campaignId;

    /// <summary>The batch id; it is also the root operation of every fact of the batch.</summary>
    public Uuid DecisionBatchId => _decisionBatchId;

    /// <summary>The materialization fact, once recorded.</summary>
    public AccessReviewDecisionBatchAccepted? Accepted => _accepted;

    /// <summary>The publication fact, once recorded.</summary>
    public AccessReviewDecisionBatchPublished? Published => _published;

    /// <summary>The accepted cancellation, once recorded.</summary>
    public AccessReviewDecisionBatchCancellationAccepted? CancellationAccepted => _cancellation;

    /// <summary>The completed cancellation, once recorded.</summary>
    public AccessReviewDecisionBatchCanceled? Canceled => _canceled;

    /// <summary>The batch's own logical revision; zero once materialized (and before it exists).</summary>
    public long Revision => _revision;

    /// <summary>True once the batch was materialized.</summary>
    public bool IsMaterialized => _accepted is not null;

    /// <summary>Every fact of the batch in order.</summary>
    public IReadOnlyList<DomainEvent> Facts => _facts;

    /// <summary>Materializes the batch as a receiver fact; the caller proved the admitted intent.</summary>
    public Result Materialize(OperationProvenance operation, ImmutableContentRef intent)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A materialization is recorded on the batch's own stream."));
        if (_accepted is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The batch is already materialized."));
        RaiseEvent(new AccessReviewDecisionBatchAccepted(Stream, 0, AccessReviewEffects.Generation, intent, null,
            operation, null, null));
        return Result.Success;
    }

    /// <summary>Publishes every provisional decision as one fact; the caller verified the complete proof set.</summary>
    public Result Publish(OperationProvenance operation, ImmutableContentRef itemProofs,
        ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(itemProofs);
        ArgumentNullException.ThrowIfNull(operationResult);
        if (_accepted is null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The batch has not been materialized yet.", true));
        if (_published is not null || _cancellation is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The batch already published or was canceled.", false));
        RaiseEvent(new AccessReviewDecisionBatchPublished(Stream, _revision + 1, AccessReviewEffects.Generation,
            _accepted.Intent, itemProofs, operation, null, operationResult));
        return Result.Success;
    }

    /// <summary>Accepts the cancellation of an unpublished batch; every delayed item effect is fenced from here.</summary>
    public Result AcceptCancellation(OperationProvenance operation, ImmutableContentRef failureProof)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(failureProof);
        if (_accepted is null || _published is not null || _cancellation is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only a materialized, unpublished batch accepts one cancellation."));
        RaiseEvent(new AccessReviewDecisionBatchCancellationAccepted(Stream, _revision + 1,
            AccessReviewEffects.Generation, _accepted.Intent, failureProof, operation, null, null));
        return Result.Success;
    }

    /// <summary>Completes the accepted cancellation once every item carries its fence.</summary>
    public Result CompleteCancellation(OperationProvenance operation, ImmutableContentRef itemFenceProofs)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(itemFenceProofs);
        if (_cancellation is null || _canceled is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only an accepted, uncompleted cancellation completes."));
        RaiseEvent(new AccessReviewDecisionBatchCanceled(Stream, _revision + 1, AccessReviewEffects.Generation,
            _accepted!.Intent, itemFenceProofs, operation, null, null));
        return Result.Success;
    }
}
