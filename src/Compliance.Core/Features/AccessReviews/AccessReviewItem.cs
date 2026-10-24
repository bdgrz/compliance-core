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
///     re-checked here against the revision.
/// </summary>
public sealed class AccessReviewItem : Aggregate, ISourceUseOwner
{
    /// <summary>Stream area of every access review item (the realm is the tenant).</summary>
    public const string Area = "access-review-items";

    readonly Uuid _tenantId;
    readonly Uuid _campaignId;
    readonly Uuid _itemId;
    readonly List<DomainEvent> _facts = [];
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
        On<AccessDecisionsRecorded>(ev => Business(ev, ev.TenantId, ev.CampaignId, ev.ItemId, ev.Revision,
            ev.Operation));
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
                "A campaign completion holds this item; retry after it settles.", true));
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

    /// <summary>Holds this item's exact pinned head for a campaign completion. The caller verified the pin.</summary>
    public Result AuthorizeCampaignCompletionUse(OperationProvenance operation, ImmutableContentRef intent,
        long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (_opened is null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The review item has not been materialized yet.", true));
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
}
