using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews.Aggregates;

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
    readonly AccessReviewReplayCut _cut = new();
    readonly Uuid _selectedBatchId;
    readonly Uuid _selectedContentEventId;
    DomainEvent? _latest;
    AccessDecisionsRecorded? _liveProvisional;
    AccessDecisionsRecorded? _selectedProvisional;
    AccessReviewItemDecisionCanceled? _selectedFence;
    bool _selectedPublished;
    DomainEvent? _selectedPin;
    long _selectedPinRevision;
    bool _onlyBulkAfterPin;
    AccessReviewItemOpened? _opened;
    long _revision;
    bool _closed;

    /// <summary>Opens the item, selecting the logical operation to resolve.</summary>
    public AccessReviewItem(Uuid tenantId, Uuid campaignId, Uuid itemId, Uuid selectedLogicalId = default,
        Uuid selectedBatchId = default, Uuid selectedContentEventId = default)
        : base(AccessReviewEffects.ItemStreamId(tenantId, campaignId, itemId),
            AccessReviewEffects.ItemStream(tenantId, campaignId, itemId))
    {
        (_tenantId, _campaignId, _itemId) = (tenantId, campaignId, itemId);
        Acceptance = new NativeAcceptance(selectedLogicalId);
        (_selectedBatchId, _selectedContentEventId) = (selectedBatchId, selectedContentEventId);
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
            ObserveBusiness(ev);
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

            if (!ev.PublicationRequired || ev.Operation.RootOperationId != batchId || IsFenced(batchId) ||
                Uses.Find(BulkKey(batchId)) is not { IsActive: true } || _liveProvisional is not null)
                throw new InvalidOperationException(
                    "A provisional bulk decision is applied once, under the batch's active hold, before any fence.");
            _liveProvisional = ev;
            if (batchId == _selectedBatchId)
                _selectedProvisional = ev;
        });
        On<AccessReviewItemDecisionAuthorized>(granted =>
        {
            Acceptance.Observe(granted);
            if (_opened is null || granted.Revision != _revision || granted.CampaignId != _campaignId ||
                granted.ItemId != _itemId || _closed || IsFenced(granted.Operation.RootOperationId))
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
            if (settled.Disposition == SourceUseSettlementDisposition.IrreversibleDecision &&
                settled.Operation.RootOperationId == _selectedBatchId)
                _selectedPublished = true;
            if (_liveProvisional?.Operation.RootOperationId == settled.Operation.RootOperationId)
                _liveProvisional = null;
        });
        On<AccessReviewItemDecisionCanceled>(fence =>
        {
            Acceptance.Observe(fence);
            if (_opened is null || fence.CampaignId != _campaignId || fence.ItemId != _itemId ||
                fence.Owner != Stream || IsFenced(fence.Operation.RootOperationId) ||
                IsPublished(fence.Operation.RootOperationId))
                throw new InvalidOperationException("An item is fenced once against a batch it did not publish.");
            if (fence.Operation.RootOperationId == _selectedBatchId)
                _selectedFence = fence;
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

    internal AccessReviewReplayCut ReplayCut => _cut;
    public Uuid TenantId => _tenantId;
    internal AccessDecisionsRecorded? LiveProvisional => _liveProvisional;
    internal Uuid SelectedBatchId => _selectedBatchId;
    internal Uuid SelectedContentEventId => _selectedContentEventId;
    internal DomainEvent? SelectedPin => _selectedPin;
    internal long SelectedPinRevision => _selectedPinRevision;
    internal bool OnlyBulkAfterPin => _onlyBulkAfterPin;

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

    /// <summary>The current business head; source-use/fence facts do not advance it.</summary>
    public DomainEvent? Latest => _latest;

    public ReservationKey BulkKey(Uuid decisionBatchId) => AccessReviewEffects.BulkUseKey(decisionBatchId,
        AccessReviewEffects.CampaignStream(_tenantId, _campaignId), Stream);

    /// <summary>Only the batch explicitly selected before hydration can supply historical authority.</summary>
    public bool IsFenced(Uuid id) => id == _selectedBatchId && _selectedFence is not null;
    public AccessReviewItemDecisionCanceled? FenceFor(Uuid id) => id == _selectedBatchId ? _selectedFence : null;
    public AccessDecisionsRecorded? ProvisionalFor(Uuid id) => id == _selectedBatchId ? _selectedProvisional :
        _liveProvisional?.Operation.RootOperationId == id ? _liveProvisional : null;
    public bool IsPublished(Uuid id) => id == _selectedBatchId && _selectedPublished;

    /// <summary>Checks the exact selected historical business head and permits only bulk-decision business tail facts.</summary>
    public bool MatchesPin(Uuid id, ulong revision) => Latest?.Metadata.EventId == id ? (ulong)_revision == revision :
        _selectedPin?.Metadata.EventId == id && (ulong)_selectedPinRevision == revision && _onlyBulkAfterPin &&
            (ulong)_revision > revision;

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
        return Append(operation, new AccessReviewItemOpened(_tenantId, _campaignId, _itemId, Stream, 1,
            AccessReviewEffects.Generation, intent, null, operation, null, null));
    }

    /// <summary>Records the reviewer's decision as the root native fact of its operation.</summary>
    public Result RecordDecision(OperationProvenance operation, ImmutableContentRef decisions,
        ImmutableContentRef intent, ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        var prepared = PrepareRoot(operation);
        if (!prepared.IsSuccess)
            return prepared;
        return Append(operation, new AccessDecisionsRecorded(_tenantId, _campaignId, _itemId, _revision + 1, decisions, operation,
            null, false, intent, firstResponse, operationResult));
    }

    /// <summary>Records a provider-side change as the root native fact of its operation.</summary>
    public Result RecordChange(OperationProvenance operation, ImmutableContentRef change,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        var prepared = PrepareRoot(operation);
        if (!prepared.IsSuccess)
            return prepared;
        return Append(operation, new AccessRemediationChangeRecorded(_tenantId, _campaignId, _revision + 1, _itemId, change,
            operation, firstResponse, operationResult));
    }

    /// <summary>Records a remediation verification as the root native fact of its operation.</summary>
    public Result Verify(OperationProvenance operation, ImmutableContentRef verification,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        var prepared = PrepareRoot(operation);
        if (!prepared.IsSuccess)
            return prepared;
        return Append(operation, new AccessRemediationVerified(_tenantId, _campaignId, _revision + 1, _itemId, verification,
            operation, firstResponse, operationResult));
    }

    /// <summary>Records a remediation exception as the root native fact of its operation.</summary>
    public Result RecordException(OperationProvenance operation, ImmutableContentRef exception,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        var prepared = PrepareRoot(operation);
        if (!prepared.IsSuccess)
            return prepared;
        return Append(operation, new AccessRemediationExceptionRecorded(_tenantId, _campaignId, _revision + 1, _itemId, exception,
            operation, firstResponse, operationResult));
    }

    /// <summary>Records a responsibility reassignment as the root native fact of its operation.</summary>
    public Result Reassign(OperationProvenance operation, ImmutableContentRef reassignment,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        var prepared = PrepareRoot(operation);
        if (!prepared.IsSuccess)
            return prepared;
        return Append(operation, new AccessReviewResponsibilityReassigned(_tenantId, _campaignId, _itemId, _revision + 1,
            reassignment, operation, firstResponse, operationResult));
    }

    /// <summary>Checks that a bulk decision may hold this item and that the item is not held by another operation.</summary>
    public Result PrepareBulkHold(Uuid decisionBatchId)
    {
        if (_opened is null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The review item has not been materialized yet; retry shortly.", true));
        if (_closed)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "A completed campaign cannot change."));
        if (decisionBatchId != _selectedBatchId)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A bulk decision must select its own root before hydration."));
        if (IsFenced(decisionBatchId))
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
        return Append(operation, new AccessReviewItemDecisionAuthorized(_tenantId, _campaignId, _itemId, Stream, _revision, generation,
            intent, null, operation, null, null));
    }

    /// <summary>Applies the batch's decision as a provisional fact under its active hold; it stays hidden until published.</summary>
    public Result ApplyBulkDecision(OperationProvenance operation, ImmutableContentRef decisions,
        ImmutableContentRef intent)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(intent);
        var batchId = operation.RootOperationId;
        if (_opened is null || _closed || batchId != _selectedBatchId || IsFenced(batchId) ||
            Uses.Find(BulkKey(batchId)) is not { IsActive: true })
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The bulk decision no longer holds this item.", false));
        if (ProvisionalFor(batchId) is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The bulk decision was already applied to this item."));
        return Append(operation, new AccessDecisionsRecorded(_tenantId, _campaignId, _itemId, _revision + 1, decisions, operation,
            batchId, true, intent, null, null));
    }

    /// <summary>Settles the batch's use of this item with the disposition its terminal batch fact requires.</summary>
    public Result SettleBulkDecision(OperationProvenance operation, ImmutableContentRef intent, long generation,
        SourceUseSettlementDisposition disposition)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.RootOperationId != _selectedBatchId)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A bulk receiver must select its own batch root before hydration."));
        Uses.RequireForAppend(SourceUsePurpose.BulkDecision, operation, generation, Version, CommittedStreamPosition);
        return Append(operation, new AccessReviewItemDecisionSettled(_tenantId, _campaignId, _itemId, Stream, _revision, generation,
            intent, null, disposition, operation, null, null));
    }

    /// <summary>Fences the item against a canceled batch, whether or not it was ever held.</summary>
    public Result FenceBulkDecision(OperationProvenance operation, ImmutableContentRef intent, long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.RootOperationId != _selectedBatchId)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A bulk receiver must select its own batch root before hydration."));
        if (_opened is null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The review item has not been materialized yet.", true));
        return Append(operation, new AccessReviewItemDecisionCanceled(_tenantId, _campaignId, _itemId, Stream, _revision, generation,
            intent, null, operation, null, null));
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
        return Append(operation, new AccessReviewItemCampaignCompletionUseAuthorized(_tenantId, _campaignId, _itemId, Stream,
            _revision, generation, intent, null, operation, null, null));
    }

    /// <summary>Settles this item's campaign completion use with the disposition its terminal source fact requires.</summary>
    public Result SettleCampaignCompletionUse(OperationProvenance operation, ImmutableContentRef intent,
        long generation, SourceUseSettlementDisposition disposition)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        Uses.RequireForAppend(SourceUsePurpose.CampaignCompletion, operation, generation, Version, CommittedStreamPosition);
        return Append(operation, new AccessReviewItemCampaignCompletionUseSettled(_tenantId, _campaignId, _itemId, Stream, _revision,
            generation, intent, null, disposition, operation, null, null));
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
        ObserveBusiness(ev);
    }
    void ObserveBusiness(DomainEvent ev)
    {
        _latest = ev;
        if (ev.Metadata.EventId == _selectedContentEventId)
        {
            _selectedPin = ev;
            _selectedPinRevision = _revision;
            _onlyBulkAfterPin = true;
        }
        else if (_selectedPin is not null && ev is not AccessDecisionsRecorded { DecisionBatchId: not null })
            _onlyBulkAfterPin = false;
    }

    Result Append(OperationProvenance operation, DomainEvent fact)
    {
        var finalized = _cut.Require(this, Acceptance, operation);
        if (!finalized.IsSuccess)
            return finalized;
        RaiseEvent(fact);
        return Result.Success;
    }

    protected override void Apply(DomainEvent ev)
    {
        if (!ev.Metadata.IsAudit)
            _cut.Observe(ev);
        base.Apply(ev);
        Uses.ObserveOwnerFact(ev);
    }
}
