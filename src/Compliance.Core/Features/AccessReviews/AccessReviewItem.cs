using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     One frozen review item of one campaign: its single materialization from the launch manifest, the reviewer
///     decision, provider changes, verification, exception and responsibility reassignments of this item only. The
///     identity derives from the tenant, campaign and item, so the stream exists once. The logical revision counts the
///     business facts of this item (the materialization is revision 1); a held campaign completion does not advance it
///     but refuses every change while the use is active, and a closed campaign keeps the item closed. The rules over
///     decision and remediation content are decided by the caller from <see cref="AccessReviewItemState" /> and
///     re-checked here against the revision. A bulk decision batch holds the item through the same source-use
///     reservations (purpose <c>bulk_decision</c>), applies its decision as a provisional fact that stays hidden until
///     the batch's publication settles the use, and a cancellation fence refuses every delayed hold or apply of the
///     batch.
/// </summary>
public sealed class AccessReviewItem : Aggregate, ISourceUseOwner
{
    /// <summary>Stream area of every access review item (the realm is the tenant).</summary>
    public const string Area = "access-review-items";

    readonly Uuid _tenantId;
    readonly Uuid _campaignId;
    readonly Uuid _itemId;
    readonly List<DomainEvent> _facts = [];
    readonly Dictionary<Uuid, AccessDecisionsRecorded> _provisional = [];
    readonly HashSet<Uuid> _published = [];
    readonly Dictionary<Uuid, AccessReviewItemDecisionCanceled> _fenced = [];
    AccessReviewItemOpened? _opened;
    long _revision;
    bool _closed;

    /// <summary>Opens the item, selecting the logical operation to resolve.</summary>
    public AccessReviewItem(Uuid tenantId, Uuid campaignId, Uuid itemId, Uuid selectedLogicalId = default)
        : base(AccessReviewEffects.ItemStreamId(tenantId, campaignId, itemId),
            AccessReviewEffects.ItemStream(tenantId, campaignId, itemId))
    {
        (_tenantId, _campaignId, _itemId) = (tenantId, campaignId, itemId);
        Acceptance = new NativeAcceptance(selectedLogicalId);
        Uses = new SourceUseReservations(Stream);
        On<AccessReviewItemOpened>(ev =>
        {
            Acceptance.Observe(ev);
            if (_opened is not null || ev.TenantId != _tenantId || ev.CampaignId != _campaignId ||
                ev.ItemId != _itemId || ev.Owner != Stream || ev.Operation.Owner != Stream || ev.Revision != 1)
                throw new InvalidOperationException(
                    "An item is materialized once, on the stream its tenant, campaign and item derive.");
            _opened = ev;
            _revision = 1;
            _facts.Add(ev);
        });
        On<AccessDecisionsRecorded>(ev =>
        {
            Business(ev, ev.TenantId, ev.CampaignId, ev.ItemId, ev.Revision, ev.Operation);
            if (ev.DecisionBatchId is not { } batchId)
            {
                if (ev.PublicationRequired)
                    throw new InvalidOperationException("Only a bulk decision awaits a publication.");
                return;
            }

            if (!ev.PublicationRequired || ev.Operation.RootOperationId != batchId || _fenced.ContainsKey(batchId) ||
                Uses.Find(BulkKey(batchId)) is not { IsActive: true } || !_provisional.TryAdd(batchId, ev))
                throw new InvalidOperationException(
                    "A provisional bulk decision is applied once, under the batch's active hold, before any fence.");
        });
        On<AccessReviewItemDecisionAuthorized>(granted =>
        {
            Acceptance.Observe(granted);
            if (_opened is null || granted.Revision != _revision || granted.CampaignId != _campaignId ||
                granted.ItemId != _itemId || _closed || _fenced.ContainsKey(granted.Operation.RootOperationId))
                throw new InvalidOperationException(
                    "A bulk decision hold is granted on an open, unfenced item at its held revision.");
            Uses.ApplyGranted(SourceUsePurpose.BulkDecision, granted);
        });
        On<AccessReviewItemDecisionSettled>(settled =>
        {
            Acceptance.Observe(settled);
            if (settled.CampaignId != _campaignId || settled.ItemId != _itemId)
                throw new InvalidOperationException("A bulk decision use settles on the item it names.");
            Uses.ApplySettled(SourceUsePurpose.BulkDecision, settled);
            if (settled.Disposition == SourceUseSettlementDisposition.IrreversibleDecision)
                _published.Add(settled.Operation.RootOperationId);
        });
        On<AccessReviewItemDecisionCanceled>(fence =>
        {
            Acceptance.Observe(fence);
            if (_opened is null || fence.CampaignId != _campaignId || fence.ItemId != _itemId ||
                fence.Owner != Stream || !_fenced.TryAdd(fence.Operation.RootOperationId, fence) ||
                _published.Contains(fence.Operation.RootOperationId))
                throw new InvalidOperationException("An item is fenced once against a batch it did not publish.");
        });
        On<AccessRemediationChangeRecorded>(ev => Business(ev, ev.TenantId, ev.CampaignId, ev.ItemId, ev.Revision,
            ev.Operation));
        On<AccessRemediationVerified>(ev => Business(ev, ev.TenantId, ev.CampaignId, ev.ItemId, ev.Revision,
            ev.Operation));
        On<AccessRemediationExceptionRecorded>(ev => Business(ev, ev.TenantId, ev.CampaignId, ev.ItemId, ev.Revision,
            ev.Operation));
        On<AccessReviewResponsibilityReassigned>(ev => Business(ev, ev.TenantId, ev.CampaignId, ev.ItemId,
            ev.Revision, ev.Operation));
        On<AccessReviewItemCampaignCompletionUseAuthorized>(granted =>
        {
            Acceptance.Observe(granted);
            if (_opened is null || granted.Revision != _revision || granted.CampaignId != _campaignId ||
                granted.ItemId != _itemId || _closed)
                throw new InvalidOperationException("A completion use is granted on an open item at its held revision.");
            Uses.ApplyGranted(SourceUsePurpose.CampaignCompletion, granted);
        });
        On<AccessReviewItemCampaignCompletionUseSettled>(settled =>
        {
            Acceptance.Observe(settled);
            if (settled.CampaignId != _campaignId || settled.ItemId != _itemId)
                throw new InvalidOperationException("A completion use settles on the item it names.");
            Uses.ApplySettled(SourceUsePurpose.CampaignCompletion, settled);
            if (settled.Disposition == SourceUseSettlementDisposition.ParentClosed)
                _closed = true;
        });
    }

    /// <summary>The active grants, receiver generation and fences of this item's source uses.</summary>
    public SourceUseReservations Uses { get; }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The campaign this item belongs to.</summary>
    public Uuid CampaignId => _campaignId;

    /// <summary>The item id within its campaign.</summary>
    public Uuid ItemId => _itemId;

    /// <summary>The materialization fact, once recorded.</summary>
    public AccessReviewItemOpened? Opened => _opened;

    /// <summary>The logical revision: the number of business facts of this item.</summary>
    public long Revision => _revision;

    /// <summary>True once a closed campaign settled its completion use of this item; no change is accepted again.</summary>
    public bool IsClosed => _closed;

    /// <summary>The business facts of the item in order.</summary>
    public IReadOnlyList<DomainEvent> Facts => _facts;

    /// <summary>The latest business fact, whose revision a campaign completion pins.</summary>
    public DomainEvent? Latest => _facts.Count == 0 ? null : _facts[^1];

    /// <summary>The reservation key of a bulk decision batch's use of this item.</summary>
    public ReservationKey BulkKey(Uuid decisionBatchId) => AccessReviewEffects.BulkUseKey(decisionBatchId,
        AccessReviewEffects.CampaignStream(_tenantId, _campaignId), Stream);

    /// <summary>True once the item was fenced against the batch.</summary>
    public bool IsFenced(Uuid decisionBatchId) => _fenced.ContainsKey(decisionBatchId);

    /// <summary>The fence fact that refuses the batch on this item, once installed.</summary>
    public AccessReviewItemDecisionCanceled? FenceFor(Uuid decisionBatchId) => _fenced.GetValueOrDefault(decisionBatchId);

    /// <summary>The provisional decision fact the batch applied to this item, if any.</summary>
    public AccessDecisionsRecorded? ProvisionalFor(Uuid decisionBatchId) =>
        _provisional.GetValueOrDefault(decisionBatchId);

    /// <summary>True once the batch's publication made its decision of this item visible.</summary>
    public bool IsPublished(Uuid decisionBatchId) => _published.Contains(decisionBatchId);

    /// <summary>A decision fact is visible unless it is a bulk decision whose batch has not published it to this item.</summary>
    public bool IsVisible(AccessDecisionsRecorded decision) =>
        decision.DecisionBatchId is not { } batchId || _published.Contains(batchId);

    /// <summary>
    ///     True when the item is still exactly at a pinned head: that fact is the latest one, or the only facts after it
    ///     are bulk decision facts (a batch admitted before the completion seal that drained since).
    /// </summary>
    public bool MatchesPin(Uuid pinnedEventId, ulong pinnedRevision)
    {
        if (Latest is not { } latest)
            return false;
        if (latest.Metadata!.EventId == pinnedEventId)
            return (ulong)_revision == pinnedRevision;
        var index = _facts.FindIndex(fact => fact.Metadata!.EventId == pinnedEventId);
        return index >= 0 && (ulong)_revision > pinnedRevision && _facts.Skip(index + 1)
            .All(static fact => fact is AccessDecisionsRecorded { DecisionBatchId: not null });
    }

    /// <summary>Checks the guards every change shares without raising anything.</summary>
    public Result PrepareChange(long expectedRevision)
    {
        if (_opened is null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The review item has not been materialized yet; retry shortly.", true));
        if (_closed)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "A completed campaign cannot change."));
        if (Uses.HasActiveUse)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "A campaign completion or a bulk decision holds this item; retry after it settles.", true));
        return expectedRevision == _revision
            ? Result.Success
            : Result.Failure(VersionedRecordRules.StaleRevision("access review item", _revision).ToRequestError());
    }

    /// <summary>Materializes the item as a receiver fact; the caller proved it was not materialized.</summary>
    public Result Materialize(OperationProvenance operation, ImmutableContentRef intent)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A materialization is recorded on the item's own stream."));
        if (_opened is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The item is already materialized."));
        RaiseEvent(new AccessReviewItemOpened(_tenantId, _campaignId, _itemId, Stream, 1,
            AccessReviewEffects.Generation, intent, null, operation, null, null));
        return Result.Success;
    }

    /// <summary>Records the reviewer's decision as the root native fact of its operation.</summary>
    public Result RecordDecision(OperationProvenance operation, ImmutableContentRef decisions,
        ImmutableContentRef intent, ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        var prepared = PrepareRoot(operation);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new AccessDecisionsRecorded(_tenantId, _campaignId, _itemId, _revision + 1, decisions, operation,
            null, false, intent, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Records a provider-side change as the root native fact of its operation.</summary>
    public Result RecordChange(OperationProvenance operation, ImmutableContentRef change,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        var prepared = PrepareRoot(operation);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new AccessRemediationChangeRecorded(_tenantId, _campaignId, _revision + 1, _itemId, change,
            operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Records a remediation verification as the root native fact of its operation.</summary>
    public Result Verify(OperationProvenance operation, ImmutableContentRef verification,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        var prepared = PrepareRoot(operation);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new AccessRemediationVerified(_tenantId, _campaignId, _revision + 1, _itemId, verification,
            operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Records a remediation exception as the root native fact of its operation.</summary>
    public Result RecordException(OperationProvenance operation, ImmutableContentRef exception,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        var prepared = PrepareRoot(operation);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new AccessRemediationExceptionRecorded(_tenantId, _campaignId, _revision + 1, _itemId, exception,
            operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Records a responsibility reassignment as the root native fact of its operation.</summary>
    public Result Reassign(OperationProvenance operation, ImmutableContentRef reassignment,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        var prepared = PrepareRoot(operation);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new AccessReviewResponsibilityReassigned(_tenantId, _campaignId, _itemId, _revision + 1,
            reassignment, operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Checks that a bulk decision may hold this item and that the item is not held by another operation.</summary>
    public Result PrepareBulkHold(Uuid decisionBatchId)
    {
        if (_opened is null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The review item has not been materialized yet; retry shortly.", true));
        if (_closed)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "A completed campaign cannot change."));
        if (_fenced.ContainsKey(decisionBatchId))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The bulk decision was canceled."));
        return Uses.HasActiveUse
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Another operation holds this item; retry after it settles.", true))
            : Result.Success;
    }

    /// <summary>Holds this item for a bulk decision batch at its current revision. The caller verified the frozen head.</summary>
    public Result AuthorizeBulkDecision(OperationProvenance operation, ImmutableContentRef intent, long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        var prepared = PrepareBulkHold(operation.RootOperationId);
        if (!prepared.IsSuccess)
            return prepared;
        Uses.RequireForAppend(SourceUsePurpose.BulkDecision, operation, generation, Version, CommittedStreamPosition);
        RaiseEvent(new AccessReviewItemDecisionAuthorized(_tenantId, _campaignId, _itemId, Stream, _revision, generation,
            intent, null, operation, null, null));
        return Result.Success;
    }

    /// <summary>Applies the batch's decision as a provisional fact under its active hold; it stays hidden until published.</summary>
    public Result ApplyBulkDecision(OperationProvenance operation, ImmutableContentRef decisions,
        ImmutableContentRef intent)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(intent);
        var batchId = operation.RootOperationId;
        if (_opened is null || _closed || _fenced.ContainsKey(batchId) ||
            Uses.Find(BulkKey(batchId)) is not { IsActive: true })
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The bulk decision no longer holds this item.", false));
        if (_provisional.ContainsKey(batchId))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The bulk decision was already applied to this item."));
        RaiseEvent(new AccessDecisionsRecorded(_tenantId, _campaignId, _itemId, _revision + 1, decisions, operation,
            batchId, true, intent, null, null));
        return Result.Success;
    }

    /// <summary>Settles the batch's use of this item with the disposition its terminal batch fact requires.</summary>
    public Result SettleBulkDecision(OperationProvenance operation, ImmutableContentRef intent, long generation,
        SourceUseSettlementDisposition disposition)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        Uses.RequireForAppend(SourceUsePurpose.BulkDecision, operation, generation, Version, CommittedStreamPosition);
        RaiseEvent(new AccessReviewItemDecisionSettled(_tenantId, _campaignId, _itemId, Stream, _revision, generation,
            intent, null, disposition, operation, null, null));
        return Result.Success;
    }

    /// <summary>Fences the item against a canceled batch, whether or not it was ever held.</summary>
    public Result FenceBulkDecision(OperationProvenance operation, ImmutableContentRef intent, long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (_opened is null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The review item has not been materialized yet.", true));
        RaiseEvent(new AccessReviewItemDecisionCanceled(_tenantId, _campaignId, _itemId, Stream, _revision, generation,
            intent, null, operation, null, null));
        return Result.Success;
    }

    /// <summary>Holds this item's exact pinned head for a campaign completion. The caller verified the pin.</summary>
    public Result AuthorizeCampaignCompletionUse(OperationProvenance operation, ImmutableContentRef intent,
        long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (_opened is null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The review item has not been materialized yet.", true));
        Uses.RequireForAppend(SourceUsePurpose.CampaignCompletion, operation, generation, Version, CommittedStreamPosition);
        RaiseEvent(new AccessReviewItemCampaignCompletionUseAuthorized(_tenantId, _campaignId, _itemId, Stream,
            _revision, generation, intent, null, operation, null, null));
        return Result.Success;
    }

    /// <summary>Settles this item's campaign completion use with the disposition its terminal source fact requires.</summary>
    public Result SettleCampaignCompletionUse(OperationProvenance operation, ImmutableContentRef intent,
        long generation, SourceUseSettlementDisposition disposition)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        Uses.RequireForAppend(SourceUsePurpose.CampaignCompletion, operation, generation, Version, CommittedStreamPosition);
        RaiseEvent(new AccessReviewItemCampaignCompletionUseSettled(_tenantId, _campaignId, _itemId, Stream, _revision,
            generation, intent, null, disposition, operation, null, null));
        return Result.Success;
    }

    Result PrepareRoot(OperationProvenance operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An item change is a root operation of its own item with an expected revision."));
        return PrepareChange((long)expected);
    }

    void Business(DomainEvent ev, Uuid tenantId, Uuid campaignId, Uuid itemId, long revision,
        OperationProvenance operation)
    {
        Acceptance.Observe(ev);
        if (_opened is null || tenantId != _tenantId || campaignId != _campaignId || itemId != _itemId ||
            revision != _revision + 1 || operation.Owner != Stream)
            throw new InvalidOperationException("An item fact follows the materialization at the next revision.");
        _revision = revision;
        _facts.Add(ev);
    }
    protected override void Apply(DomainEvent ev)
    {
        base.Apply(ev);
        Uses.ObserveOwnerFact(ev);
    }
}
