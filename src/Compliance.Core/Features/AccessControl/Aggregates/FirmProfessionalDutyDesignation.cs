using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     One professional-duty designation of the platform realm, identified by the canonical user, the duty and the
///     explicit tenant scope (never an empty tenant UUID). It owns one active signed designation, the monotonic signed
///     authority revision and the continuity proof carried by the latest designation content. The logical revision
///     counts applied changes. At most one accepted change is pending; it ends as recorded, revoked or rejected.
/// </summary>
public sealed class FirmProfessionalDutyDesignation : Aggregate, ISourceUseOwner
{
    /// <summary>Stream area of every designation owner.</summary>
    public const string Area = "firm-professional-duty-designations";

    ProfessionalDutyChangeAccepted? _pending;
    ProfessionalDutyChangeAccepted? _selectedAccepted;
    DomainEvent? _selectedTerminal;
    DomainEvent? _latest;
    ulong _revision;

    /// <summary>Opens the designation of one subject, duty and scope, selecting the logical operation to resolve.</summary>
    public FirmProfessionalDutyDesignation(Uuid userId, string duty, Uuid? tenantId, Uuid selectedLogicalId = default, Uuid selectedRootId = default)
        : base(IdentityOf(userId, duty, tenantId),
            new EventStreamAddress(ProfessionalDutyContent.Realm, Area, IdentityOf(userId, duty, tenantId).ToString()))
    {
        UserId = userId;
        Duty = duty;
        TenantId = tenantId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        SelectedRootId = selectedRootId == Uuid.Empty ? selectedLogicalId : selectedRootId;
        Uses = new SourceUseReservations(Stream);
        On<ProfessionalDutyChangeAccepted>(accepted =>
        {
            Acceptance.Observe(accepted);
            if (_pending is not null || accepted.Owner != Stream || accepted.Operation.Owner != Stream ||
                accepted.Operation.LogicalId != accepted.Operation.RootOperationId ||
                accepted.Revision != (long)_revision || accepted.FirstResponse is null)
                throw new InvalidOperationException(
                    "A designation accepts one new change at a time, at its own revision.");
            _pending = accepted;
            if (accepted.Operation.RootOperationId == SelectedRootId)
                _selectedAccepted = accepted;
        });
        On<FirmProfessionalDutyDesignationRecorded>(recorded =>
        {
            Acceptance.Observe(recorded);
            Complete(recorded, recorded.Operation, recorded.OperationId, recorded.Intent, recorded.Revision);
        });
        On<FirmProfessionalDutyDesignationRevoked>(revoked =>
        {
            Acceptance.Observe(revoked);
            if (_latest is null)
                throw new InvalidOperationException("Only a recorded designation can be revoked.");
            Complete(revoked, revoked.Operation, revoked.OperationId, revoked.Intent, revoked.Revision);
        });
        On<ProfessionalDutyChangeRejected>(rejected =>
        {
            Acceptance.Observe(rejected);
            if (_pending?.Operation.RootOperationId != rejected.Operation.RootOperationId || rejected.Owner != Stream ||
                rejected.Operation.Owner != Stream || rejected.Intent != _pending!.Intent)
                throw new InvalidOperationException("Only the exact pending change can be rejected.");
            if (rejected.Operation.RootOperationId == SelectedRootId)
                _selectedTerminal = rejected;
            _pending = null;
        });
        On<FirmProfessionalDutyDesignationEngagementUseAuthorized>(granted =>
        {
            Acceptance.Observe(granted);
            if (_latest is null || _pending is not null || granted.Revision != (long)_revision)
                throw new InvalidOperationException("A designation use is granted on a recorded designation at its held revision.");
            Uses.ApplyGranted(SourceUsePurpose.EngagementPersonalDecision, granted);
        });
        On<FirmProfessionalDutyDesignationEngagementUseSettled>(settled =>
        {
            Acceptance.Observe(settled);
            Uses.ApplySettled(SourceUsePurpose.EngagementPersonalDecision, settled);
        });
    }

    internal FirmIndependenceReplayCut ReplayCut { get; } = new();

    /// <summary>The single historical root deliberately selected for this hydration.</summary>
    public Uuid SelectedRootId { get; }

    /// <summary>The active grants, receiver generation and fences of this designation's source uses.</summary>
    public SourceUseReservations Uses { get; }

    /// <summary>The canonical user the designation names.</summary>
    public Uuid UserId { get; }

    /// <summary>The duty, one of <see cref="FirmProfessionalDuty" />.</summary>
    public string Duty { get; }

    /// <summary>The tenant scope, or null for the explicit platform scope.</summary>
    public Uuid? TenantId { get; }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The logical revision: the number of applied changes.</summary>
    public ulong Revision => _revision;

    /// <summary>The accepted change that is neither applied nor rejected yet.</summary>
    public ProfessionalDutyChangeAccepted? Pending => _pending;

    /// <summary>The latest recorded or revoked fact, whose designation content is the current view.</summary>
    public INativeOperationFact? Latest => _latest as INativeOperationFact;

    /// <summary>The immutable designation content of the latest applied change.</summary>
    public ImmutableContentRef? LatestContent => _latest switch
    {
        FirmProfessionalDutyDesignationRecorded recorded => recorded.DesignationContent,
        FirmProfessionalDutyDesignationRevoked revoked => revoked.DesignationContent,
        _ => null,
    };

    /// <summary>The signed assertion of the latest applied change.</summary>
    public string? LatestSignedAssertion => _latest switch
    {
        FirmProfessionalDutyDesignationRecorded recorded => recorded.SignedAssertion,
        FirmProfessionalDutyDesignationRevoked revoked => revoked.SignedAssertion,
        _ => null,
    };

    /// <summary>The identity of the owner of one subject, duty and scope; the platform scope is explicit.</summary>
    public static Uuid IdentityOf(Uuid userId, string duty, Uuid? tenantId) =>
        AggregateIdentity.Create(nameof(FirmProfessionalDutyDesignation), userId, duty,
            tenantId is { } tenant ? $"tenant:{tenant}" : "platform");

    /// <summary>The pending or explicitly selected accepted change of an assertion.</summary>
    public ProfessionalDutyChangeAccepted? AcceptedFor(Uuid assertionId) =>
        _pending?.Operation.RootOperationId == assertionId ? _pending :
        SelectedRootId == assertionId ? _selectedAccepted : null;

    /// <summary>The selected terminal receipt, or the current applied decision when it names this assertion.</summary>
    public DomainEvent? TerminalFor(Uuid assertionId) =>
        SelectedRootId == assertionId ? _selectedTerminal :
        (_latest as INativeOperationFact)?.Operation.RootOperationId == assertionId ? _latest : null;

    /// <summary>True when the change of an assertion was rejected.</summary>
    public bool IsRejected(Uuid assertionId) => TerminalFor(assertionId) is ProfessionalDutyChangeRejected;

    /// <summary>True when the change of an assertion was recorded or revoked.</summary>
    public bool IsApplied(Uuid assertionId) =>
        TerminalFor(assertionId) is FirmProfessionalDutyDesignationRecorded or FirmProfessionalDutyDesignationRevoked;

    /// <summary>Checks the admission guards without raising anything.</summary>
    public Result PrepareChange(long expectedRevision, Uuid assertionId)
    {
        if (AcceptedFor(assertionId) is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "This signed assertion identity was already accepted for this designation."));
        if (_pending is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Another professional-duty change is still being applied; retry after it ends.", true));
        if (Uses.HasActiveUse)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "An engagement personal decision holds this designation; retry after it settles.", true));
        return expectedRevision == (long)_revision
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Reload the professional-duty designation; the expected revision is stale."));
    }

    /// <summary>Accepts the change as the root native fact of its operation.</summary>
    public Result AcceptChange(OperationProvenance operation, ImmutableContentRef intent,
        ImmutableContentRef firstResponse)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(firstResponse);
        if (operation.Owner != Stream || operation.LogicalId != operation.RootOperationId)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A professional-duty change is the logical operation of its own signed assertion."));
        var prepared = PrepareChange(operation.OriginalExpectedRevision is { } expected ? (long)expected : -1,
            operation.LogicalId);
        if (!prepared.IsSuccess)
            return prepared;
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        RaiseEvent(new ProfessionalDutyChangeAccepted(Stream, (long)_revision, ProfessionalDutyEffects.Generation,
            intent, null, operation, firstResponse, null));
        return Result.Success;
    }

    /// <summary>Holds this exact recorded designation for an engagement personal decision; the revision does not advance.</summary>
    public Result AuthorizeEngagementUse(OperationProvenance operation, ImmutableContentRef intent, long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (_latest is null || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only a recorded designation can be held, on its own stream.", true));
        if (_pending is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "A signed designation change is already pending; retry after it settles.", true));
        Uses.RequireForAppend(SourceUsePurpose.EngagementPersonalDecision, operation, generation, Version, CommittedStreamPosition);
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        RaiseEvent(new FirmProfessionalDutyDesignationEngagementUseAuthorized(Stream, (long)_revision, generation,
            intent, null, operation, null, null));
        return Result.Success;
    }

    /// <summary>Settles an engagement use, or fences its key when no grant exists yet.</summary>
    public Result SettleEngagementUse(OperationProvenance operation, ImmutableContentRef intent, long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A settlement is recorded on the held designation stream."));
        Uses.RequireForAppend(SourceUsePurpose.EngagementPersonalDecision, operation, generation, Version, CommittedStreamPosition);
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        RaiseEvent(new FirmProfessionalDutyDesignationEngagementUseSettled(Stream, (long)_revision, generation,
            intent, null, operation, null, null));
        return Result.Success;
    }

    /// <summary>Applies the pending change; the permanent assertion claim and any staff use were proven by the caller.</summary>
    public Result Apply(ProfessionalDutyChangeKind kind, OperationProvenance operation,
        ImmutableContentRef designationContent, string signedAssertion, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (_pending?.Operation.RootOperationId != operation.RootOperationId)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only the exact pending change can be applied."));
        if (Uses.HasActiveUse)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "An engagement personal decision holds this exact designation; retry after it settles.", true));
        var intent = _pending!.Intent;
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        if (kind == ProfessionalDutyChangeKind.Record)
            RaiseEvent(new FirmProfessionalDutyDesignationRecorded(operation.RootOperationId, designationContent,
                signedAssertion, operation, _revision + 1, intent, null, operationResult));
        else
            RaiseEvent(new FirmProfessionalDutyDesignationRevoked(operation.RootOperationId, designationContent,
                signedAssertion, operation, _revision + 1, intent, null, operationResult));
        return Result.Success;
    }

    /// <summary>Rejects the pending change; an applied change is never rejected.</summary>
    public Result Reject(OperationProvenance operation, ImmutableContentRef failureProof)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (_pending?.Operation.RootOperationId != operation.RootOperationId)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only the exact pending change can be rejected."));
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        RaiseEvent(new ProfessionalDutyChangeRejected(Stream, (long)_revision, ProfessionalDutyEffects.Generation,
            _pending!.Intent, failureProof, operation, null, null));
        return Result.Success;
    }

    void Complete(DomainEvent fact, OperationProvenance operation, Uuid operationId, ImmutableContentRef intent,
        ulong revision)
    {
        if (_pending is not { } pending || pending.Operation.RootOperationId != operation.RootOperationId ||
            operationId != pending.Operation.RootOperationId || operation.Owner != Stream || intent != pending.Intent || revision != _revision + 1)
            throw new InvalidOperationException("A designation change must follow its accepted change and revision.");
        if (operation.RootOperationId == SelectedRootId)
            _selectedTerminal = fact;
        _latest = fact;
        _revision = revision;
        _pending = null;
    }
    protected override void Apply(DomainEvent ev)
    {
        if (!ev.Metadata.IsAudit)
            ReplayCut.Observe(ev);
        base.Apply(ev);
        Uses.ObserveOwnerFact(ev);
    }
}
