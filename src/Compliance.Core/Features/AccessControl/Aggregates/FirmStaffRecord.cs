using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     One firm staff member: the frozen registration, its completion or rejection, and the status changes of this
///     record only. The logical revision counts completed changes; it is independent of every other record. The
///     record embeds <see cref="SourceUseReservations" />: a held professional-duty use does not advance the revision
///     but refuses status changes until it is settled.
/// </summary>
public sealed class FirmStaffRecord : Aggregate, ISourceUseOwner
{
    /// <summary>Stream area of every staff record.</summary>
    public const string Area = "firm-staff-records";

    FirmStaffRegistrationAccepted? _accepted;
    FirmStaffRegistrationRejected? _rejected;
    FirmStaffChangeRecorded? _latest;
    ulong _revision;

    /// <summary>Opens the record of one staff member, selecting the logical operation to resolve.</summary>
    public FirmStaffRecord(Uuid staffMemberId, Uuid selectedLogicalId = default)
        : base(AggregateIdentity.Create(nameof(FirmStaffRecord), staffMemberId),
            new EventStreamAddress(FirmStaffContent.Realm, Area, staffMemberId.ToString()))
    {
        StaffMemberId = staffMemberId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        Uses = new SourceUseReservations(Stream);
        On<FirmStaffRegistrationAccepted>(accepted =>
        {
            Acceptance.Observe(accepted);
            if (_accepted is not null || accepted.Owner != Stream || accepted.Operation.Owner != Stream ||
                accepted.Operation.LogicalId != StaffMemberId || accepted.Operation.RootOperationId != StaffMemberId ||
                accepted.Revision != 0 || accepted.FirstResponse is null)
                throw new InvalidOperationException("A staff record accepts exactly one registration of its own staff member.");
            _accepted = accepted;
        });
        On<FirmStaffRegistrationRejected>(rejected =>
        {
            Acceptance.Observe(rejected);
            if (_accepted is null || _rejected is not null || _latest is not null || rejected.Owner != Stream ||
                rejected.Operation.Owner != Stream || rejected.Intent != _accepted.Intent ||
                rejected.Operation.RootOperationId != StaffMemberId)
                throw new InvalidOperationException("Only the exact pending registration can be rejected.");
            _rejected = rejected;
        });
        On<FirmStaffChangeRecorded>(change =>
        {
            Acceptance.Observe(change);
            var registering = change.ChangeKind == "register";
            if (_accepted is null || _rejected is not null || change.Operation.Owner != Stream ||
                change.Intent != _accepted.Intent || change.Revision != _revision + 1 ||
                registering != (_latest is null) || change.ChangeKind is not ("register" or "status"))
                throw new InvalidOperationException("Staff changes must follow the accepted registration and the previous revision.");
            _latest = change;
            _revision = change.Revision;
        });
        On<FirmStaffProfessionalDutyUseAuthorized>(granted =>
        {
            Acceptance.Observe(granted);
            if (_latest is null || granted.Revision != (long)_revision)
                throw new InvalidOperationException("A staff use is granted on a registered record at its held revision.");
            Uses.ApplyGranted(SourceUsePurpose.ProfessionalDutyRecording, granted);
        });
        On<FirmStaffProfessionalDutyUseSettled>(settled =>
        {
            Acceptance.Observe(settled);
            Uses.ApplySettled(SourceUsePurpose.ProfessionalDutyRecording, settled);
        });
        On<FirmStaffRecordEngagementUseAuthorized>(granted =>
        {
            Acceptance.Observe(granted);
            if (_latest is null || granted.Revision != (long)_revision)
                throw new InvalidOperationException("A staff use is granted on a registered record at its held revision.");
            Uses.ApplyGranted(SourceUsePurpose.EngagementPersonalDecision, granted);
        });
        On<FirmStaffRecordEngagementUseSettled>(settled =>
        {
            Acceptance.Observe(settled);
            Uses.ApplySettled(SourceUsePurpose.EngagementPersonalDecision, settled);
        });
    }

    /// <summary>The active grants, receiver generation and fences of this record's source uses.</summary>
    public SourceUseReservations Uses { get; }

    /// <summary>The staff member this record owns.</summary>
    public Uuid StaffMemberId { get; }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The root acceptance of the registration, once recorded.</summary>
    public FirmStaffRegistrationAccepted? Accepted => _accepted;

    /// <summary>True once a registration was accepted and neither completed nor rejected.</summary>
    public bool IsPending => _accepted is not null && _rejected is null && _latest is null;

    /// <summary>True once the registration completed.</summary>
    public bool IsRegistered => _latest is not null;

    /// <summary>True once the pending registration was rejected.</summary>
    public bool IsRejected => _rejected is not null;

    /// <summary>The logical revision: the number of completed changes of this record.</summary>
    public ulong Revision => _revision;

    /// <summary>The frozen registration intent, once accepted.</summary>
    public ImmutableContentRef? RegistrationIntent => _accepted?.Intent;

    /// <summary>The latest change, whose staff content is the current view.</summary>
    public FirmStaffChangeRecorded? Latest => _latest;

    internal FirmIndependenceReplayCut ReplayCut { get; } = new();

    /// <summary>Checks the admission guards without raising anything.</summary>
    public Result PrepareRegistration(long expectedRevision)
    {
        if (_accepted is not null || _revision != 0)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "This staff member already has a registration; staff identities are immutable."));
        if (expectedRevision != 0)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "A new staff record is created at revision zero."));
        return Result.Success;
    }

    /// <summary>Accepts the registration as the root native fact of its operation.</summary>
    public Result AcceptRegistration(OperationProvenance operation, ImmutableContentRef intent,
        ImmutableContentRef firstResponse)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(firstResponse);
        if (operation.LogicalId != StaffMemberId || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A staff registration is the logical operation of its own staff record."));
        var prepared = PrepareRegistration(operation.OriginalExpectedRevision is { } expected ? (long)expected : -1);
        if (!prepared.IsSuccess)
            return prepared;
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        RaiseEvent(new FirmStaffRegistrationAccepted(Stream, 0, FirmStaffEffects.Generation, intent, null, operation,
            firstResponse, null));
        return Result.Success;
    }

    /// <summary>Completes the pending registration; the permanent claim was proven by the caller.</summary>
    public Result CompleteRegistration(OperationProvenance operation, ImmutableContentRef staffContent,
        ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (!IsPending)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only a pending registration can be completed."));
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        RaiseEvent(new FirmStaffChangeRecorded(StaffMemberId, "register", null, staffContent, operation, 1,
            _accepted!.Intent, null, operationResult));
        return Result.Success;
    }

    /// <summary>Rejects the pending registration; a completed registration is never rejected.</summary>
    public Result RejectRegistration(OperationProvenance operation, ImmutableContentRef failureProof)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (!IsPending)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only a pending registration can be rejected."));
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        RaiseEvent(new FirmStaffRegistrationRejected(Stream, 0, FirmStaffEffects.Generation, _accepted!.Intent,
            failureProof, operation, null, null));
        return Result.Success;
    }

    /// <summary>Checks the status guards without raising anything.</summary>
    public Result PrepareStatus(ulong expectedRevision)
    {
        if (IsPending)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The staff registration is not complete yet.", true));
        if (!IsRegistered)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The staff member was not found."));
        if (Uses.HasActiveUse)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "A professional-duty change or an engagement personal decision holds this staff record; retry after it settles.", true));
        return expectedRevision == _revision
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Reload the staff record before changing its status."));
    }

    /// <summary>Records a status change as the root native fact of its operation.</summary>
    public Result SetStatus(OperationProvenance operation, string reason, ImmutableContentRef staffContent,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A status change requires its own operation and expected revision."));
        var prepared = PrepareStatus(expected);
        if (!prepared.IsSuccess)
            return prepared;
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        RaiseEvent(new FirmStaffChangeRecorded(operation.LogicalId, "status", reason, staffContent, operation,
            _revision + 1, _accepted!.Intent, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Holds this exact registered record for a professional-duty recording; the revision does not advance.</summary>
    public Result AuthorizeProfessionalDutyUse(OperationProvenance operation, ImmutableContentRef intent,
        long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (!IsRegistered || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only a registered staff record can be held, on its own stream.", true));
        Uses.RequireForAppend(SourceUsePurpose.ProfessionalDutyRecording, operation, generation, Version, CommittedStreamPosition);
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        RaiseEvent(new FirmStaffProfessionalDutyUseAuthorized(Stream, (long)_revision, generation, intent, null,
            operation, null, null));
        return Result.Success;
    }

    /// <summary>Holds this exact registered record for an engagement personal decision; the revision does not advance.</summary>
    public Result AuthorizeEngagementUse(OperationProvenance operation, ImmutableContentRef intent, long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (!IsRegistered || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only a registered staff record can be held, on its own stream.", true));
        Uses.RequireForAppend(SourceUsePurpose.EngagementPersonalDecision, operation, generation, Version, CommittedStreamPosition);
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        RaiseEvent(new FirmStaffRecordEngagementUseAuthorized(Stream, (long)_revision, generation, intent, null,
            operation, null, null));
        return Result.Success;
    }

    /// <summary>Settles an engagement use, or fences its key when no grant exists yet.</summary>
    public Result SettleEngagementUse(OperationProvenance operation, ImmutableContentRef intent, long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A settlement is recorded on the held staff stream."));
        Uses.RequireForAppend(SourceUsePurpose.EngagementPersonalDecision, operation, generation, Version, CommittedStreamPosition);
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        RaiseEvent(new FirmStaffRecordEngagementUseSettled(Stream, (long)_revision, generation, intent, null,
            operation, null, null));
        return Result.Success;
    }

    /// <summary>Settles a professional-duty use, or fences its key when no grant exists yet.</summary>
    public Result SettleProfessionalDutyUse(OperationProvenance operation, ImmutableContentRef intent, long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A settlement is recorded on the held staff stream."));
        Uses.RequireForAppend(SourceUsePurpose.ProfessionalDutyRecording, operation, generation, Version, CommittedStreamPosition);
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        RaiseEvent(new FirmStaffProfessionalDutyUseSettled(Stream, (long)_revision, generation, intent, null,
            operation, null, null));
        return Result.Success;
    }
    protected override void Apply(DomainEvent ev)
    {
        if (!ev.Metadata.IsAudit)
            ReplayCut.Observe(ev);
        base.Apply(ev);
        Uses.ObserveOwnerFact(ev);
    }
}
