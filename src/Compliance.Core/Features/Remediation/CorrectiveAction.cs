using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     One corrective action of a finding, identified by its finding and action. It is materialized from the
///     finding's admission at revision one, completed at revision two, and held by finding closures through
///     <see cref="SourceUseReservations" />: an active use does not advance the public revision but blocks completion
///     until it is settled, and a settlement for a closed parent installs the closed-generation fence that releasing
///     the temporary use never removes. The logical revision counts content facts of this action only.
/// </summary>
public sealed class CorrectiveAction : Aggregate, ISourceUseOwner
{
    /// <summary>Stream area of every corrective action (the realm is the tenant).</summary>
    public const string Area = "corrective-actions";

    /// <summary>The identity owner type of a corrective action.</summary>
    public const string OwnerType = "CorrectiveAction";

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    readonly Uuid _findingId;
    readonly Uuid _actionId;
    readonly List<DomainEvent> _facts = [];
    readonly SortedSet<long> _closedGenerations = [];
    CorrectiveActionAddedV2? _added;
    CorrectiveActionCompletedV2? _completed;
    long _revision;

    /// <summary>Opens one action, selecting the logical operation to resolve.</summary>
    public CorrectiveAction(Uuid tenantId, Uuid programId, Uuid findingId, Uuid actionId, Uuid selectedLogicalId = default)
        : base(IdentityOf(tenantId, programId, findingId, actionId),
            new EventStreamAddress(tenantId.ToString(), Area, IdentityOf(tenantId, programId, findingId, actionId).ToString()))
    {
        (_tenantId, _programId, _findingId, _actionId) = (tenantId, programId, findingId, actionId);
        Acceptance = new NativeAcceptance(selectedLogicalId);
        Uses = new SourceUseReservations(Stream);
        On<CorrectiveActionAddedV2>(Apply);
        On<CorrectiveActionCompletedV2>(Apply);
        On<CorrectiveActionFindingClosureUseAuthorized>(granted =>
        {
            Acceptance.Observe(granted);
            if (_added is null || granted.Revision != _revision)
                throw new InvalidOperationException("A closure use is granted on a materialized action at its held revision.");
            Uses.ApplyGranted(SourceUsePurpose.FindingClosure, granted);
            _facts.Add(granted);
        });
        On<CorrectiveActionFindingClosureUseSettled>(settled =>
        {
            Acceptance.Observe(settled);
            Uses.ApplySettled(SourceUsePurpose.FindingClosure, settled);
            if (settled.Disposition == SourceUseSettlementDisposition.ParentClosed)
                _closedGenerations.Add(settled.Generation);
            _facts.Add(settled);
        });
    }

    /// <summary>The identity of the action of a finding.</summary>
    public static Uuid IdentityOf(Uuid tenantId, Uuid programId, Uuid findingId, Uuid actionId) =>
        AggregateIdentity.Create(OwnerType, tenantId, programId, findingId, actionId);

    /// <inheritdoc />
    public SourceUseReservations Uses { get; }

    /// <inheritdoc />
    public NativeAcceptance Acceptance { get; }

    /// <summary>The action identity.</summary>
    public Uuid ActionId => _actionId;

    /// <summary>The finding this action belongs to.</summary>
    public Uuid FindingId => _findingId;

    /// <summary>The logical revision: zero before materialization, one when open, two when completed.</summary>
    public long Revision => _revision;

    /// <summary>True once the action was materialized.</summary>
    public bool IsMaterialized => _added is not null;

    /// <summary>True once the action was completed.</summary>
    public bool IsCompleted => _completed is not null;

    /// <summary>The materialization fact, once recorded.</summary>
    public CorrectiveActionAddedV2? Added => _added;

    /// <summary>The completion fact, once recorded.</summary>
    public CorrectiveActionCompletedV2? Completed => _completed;

    /// <summary>The latest content fact: the completion when completed, else the materialization.</summary>
    public DomainEvent? Latest => (DomainEvent?)_completed ?? _added;

    /// <summary>The finding generations whose closure fenced this action.</summary>
    public IReadOnlyCollection<long> ClosedGenerations => _closedGenerations;

    /// <summary>The fact with this event id.</summary>
    public DomainEvent? FactById(Uuid eventId) => _facts.FirstOrDefault(fact => fact.Metadata?.EventId == eventId);

    /// <summary>Checks that the action can be materialized.</summary>
    public Result PrepareMaterialize() => _added is null
        ? Result.Success
        : Result.Failure(new RequestError(RequestErrorKind.Conflict, "The corrective action is already materialized."));

    /// <summary>Materializes the admitted action as a receiver fact at revision one.</summary>
    public Result Materialize(OperationProvenance operation, ImmutableContentRef actionContent, ImmutableContentRef intent)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var prepared = PrepareMaterialize();
        if (!prepared.IsSuccess)
            return prepared;
        if (operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A materialization is recorded on the action's own stream."));
        RaiseEvent(new CorrectiveActionAddedV2(operation.LogicalId, _tenantId, _programId, _findingId, 1,
            actionContent, operation, intent, null, null));
        return Result.Success;
    }

    /// <summary>Checks that the action can be completed at the expected revision.</summary>
    public Result PrepareComplete(long expectedRevision)
    {
        if (_added is null)
            return Fail(CommandFailure.MissingRecord("The corrective action was not found."));
        if (_closedGenerations.Count > 0)
            return Fail(CommandFailure.StateConflict(
                "The corrective action is fenced by a closed finding generation."));
        if (Uses.HasActiveUse)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "A finding closure holds this corrective action; retry when it settles.", true));
        if (expectedRevision != _revision)
            return Fail(CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("corrective action", _revision)));
        return _completed is not null
            ? Fail(CommandFailure.StateConflict("The corrective action is already completed."))
            : Result.Success;
    }

    /// <summary>Records the completion as the root native fact of its operation.</summary>
    public Result Complete(OperationProvenance operation, string resolutionNotes, ImmutableContentRef evidenceManifest,
        Uuid completedByMemberId, AccessControl.ActorReference completedBy, DateTimeOffset completedAt,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A completion is a root operation of its own action with an expected revision."));
        var prepared = PrepareComplete((long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new CorrectiveActionCompletedV2(operation.LogicalId, _tenantId, _programId, _findingId,
            _revision + 1, _actionId, resolutionNotes, evidenceManifest, completedByMemberId, completedBy,
            completedAt, operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Holds this exact completed action for a finding closure; the revision does not advance.</summary>
    public Result AuthorizeFindingClosureUse(OperationProvenance operation, ImmutableContentRef intent, long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (_added is null || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only a materialized action can be held, on its own stream.", true));
        Uses.RequireForAppend(SourceUsePurpose.FindingClosure, operation, generation, Version, CommittedStreamPosition);
        RaiseEvent(new CorrectiveActionFindingClosureUseAuthorized(Stream, _revision, generation, intent, null,
            operation, null, null));
        return Result.Success;
    }

    /// <summary>Settles a closure use, or fences its key when no grant exists yet.</summary>
    public Result SettleFindingClosureUse(OperationProvenance operation, ImmutableContentRef intent, long generation,
        SourceUseSettlementDisposition disposition)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A settlement is recorded on the action's own stream."));
        Uses.RequireForAppend(SourceUsePurpose.FindingClosure, operation, generation, Version, CommittedStreamPosition);
        RaiseEvent(new CorrectiveActionFindingClosureUseSettled(Stream, _revision, generation, intent, null,
            operation, null, null, disposition));
        return Result.Success;
    }

    void Apply(CorrectiveActionAddedV2 ev)
    {
        Acceptance.Observe(ev);
        if (_added is not null || ev.TenantId != _tenantId || ev.ProgramId != _programId || ev.FindingId != _findingId ||
            ev.Revision != 1 || ev.Operation.Owner != Stream)
            throw new InvalidOperationException("An action is materialized once, at revision one, on its own stream.");
        _added = ev;
        _revision = 1;
        _facts.Add(ev);
    }

    void Apply(CorrectiveActionCompletedV2 ev)
    {
        Acceptance.Observe(ev);
        if (_added is null || _completed is not null || ev.ActionId != _actionId || ev.FindingId != _findingId ||
            ev.Revision != _revision + 1 || ev.Operation.Owner != Stream)
            throw new InvalidOperationException("An action completes once, at the next revision after its materialization.");
        _completed = ev;
        _revision = ev.Revision;
        _facts.Add(ev);
    }

    static Result Fail(CommandFailure failure) => Result.Failure(failure.ToRequestError());
    protected override void Apply(DomainEvent ev)
    {
        base.Apply(ev);
        Uses.ObserveOwnerFact(ev);
    }
}
