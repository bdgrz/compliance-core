using System.Globalization;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

/// <summary>Current program authority and one selected native acceptance; all criteria history composes outside it.</summary>
public sealed class ComplianceProgram : Aggregate
{
    public const string Area = "programs";
    readonly Uuid _tenantId;
    bool _created;
    long _revision;
    bool _lastChangeWasSelection;
    ProgramCriteriaEditionSelected? _selection;
    ImmutableContentRef? _content;
    ulong _nativeSequence;

    public ComplianceProgram(Uuid tenantId, Uuid programId, Uuid selectedLogicalId = default)
        : base(programId, new EventStreamAddress(tenantId.ToString(), Area, programId.ToString()))
    {
        _tenantId = tenantId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<ProgramCreated>(ApplyFact);
        On<ProgramRevised>(ApplyFact);
        On<ProgramCriteriaEditionSelected>(ApplyFact);
    }

    public Uuid TenantId => _tenantId;
    public bool IsCreated => _created;
    public long Revision => _revision;
    public Uuid? CriteriaEditionId => _selection?.EditionId;
    public ProgramCriteriaEditionSelected? CurrentSelection => _selection;
    public ImmutableContentRef? CurrentContent => _content;
    public ulong NativeSequence => _nativeSequence;
    public bool LastChangeWasSelection => _lastChangeWasSelection;
    public NativeAcceptance Acceptance { get; }
    bool _emitting;
    internal NativeReplayCommitment NativeCut { get; } = new();

    public Result PrepareCreate() => !_created ? Result.Success : Failure("The program already exists with different content.");

    public Result PrepareRevise(long expectedRevision) => !_created
        ? Result.Failure(new RequestError(RequestErrorKind.NotFound, "The program was not found."))
        : expectedRevision != Revision ? Stale() : Result.Success;

    public Result<bool> PrepareSelection(long expectedRevision, Uuid editionId)
    {
        if (!_created)
            return Result<bool>.Failure(new RequestError(RequestErrorKind.NotFound, "The program was not found."));
        if (editionId == Uuid.Empty)
            return Result<bool>.Failure(new RequestError(RequestErrorKind.Validation, "A criteria edition is required."));
        if (CriteriaEditionId == editionId && (expectedRevision == Revision || _lastChangeWasSelection && expectedRevision + 1 == Revision))
            return Result<bool>.Success(false);
        return expectedRevision != Revision ? Result<bool>.Failure(Stale().Error!) : Result<bool>.Success(true);
    }

    public Result Create(OperationProvenance operation, ImmutableContentRef content, ImmutableContentRef first,
        ImmutableContentRef result)
    {
        var guard = NativeCut.Require(this, Acceptance, operation);
        if (!guard.IsSuccess)
            return guard;
        var prepared = PrepareCreate();
        if (!prepared.IsSuccess)
            return prepared;
        Emit(new ProgramCreated(_tenantId, Id, 1, content, operation.AcceptedAt, operation, first, result, NativeSequence + 1, 1));
        return Result.Success;
    }

    public Result Revise(OperationProvenance operation, ImmutableContentRef content, ImmutableContentRef first,
        ImmutableContentRef result)
    {
        var guard = NativeCut.Require(this, Acceptance, operation);
        if (!guard.IsSuccess)
            return guard;
        if (operation.OriginalExpectedRevision is not { } expected || expected > long.MaxValue)
            return Failure("A program revision requires its original business revision.");
        var prepared = PrepareRevise((long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        Emit(new ProgramRevised(_tenantId, Id, Revision + 1, content, operation.AcceptedAt, operation, first, result, NativeSequence + 1, 1));
        return Result.Success;
    }

    public Result SelectCriteriaEdition(OperationProvenance operation, Uuid editionId, ImmutableContentRef first,
        ImmutableContentRef result)
    {
        var guard = NativeCut.Require(this, Acceptance, operation);
        if (!guard.IsSuccess)
            return guard;
        if (operation.OriginalExpectedRevision is not { } expected || expected > long.MaxValue)
            return Failure("A criteria selection requires its original business revision.");
        var prepared = PrepareSelection((long)expected, editionId);
        if (!prepared.IsSuccess)
            return Result.Failure(prepared.Error!);
        var changed = prepared.Value;
        Emit(new ProgramCriteriaEditionSelected(_tenantId, Id, Revision + (changed ? 1 : 0), editionId, _content!,
            changed ? operation.AcceptedAt : _selection!.ChangedAt, operation, first, result, NativeSequence + 1, changed ? 1 : 0));
        return Result.Success;
    }

    internal void ObserveVerified(DomainEvent ev)
    {
        ApplyFact((IProgramFact)ev);
        NativeCut.Observe(ev, false);
    }

    protected override void Apply(DomainEvent ev)
    {
        base.Apply(ev);
        if (!ev.Metadata.IsAudit)
            NativeCut.Observe(ev, _emitting);
    }

    void Emit(DomainEvent fact)
    {
        _emitting = true;
        try
        { RaiseEvent(fact); }
        finally { _emitting = false; }
    }

    void ApplyFact(IProgramFact fact)
    {
        ProgramHistory.ValidateHeader(fact);
        var kind = fact switch
        {
            ProgramCreated => nameof(CreateProgram),
            ProgramRevised => nameof(ReviseProgram),
            ProgramCriteriaEditionSelected => nameof(SelectProgramCriteriaEdition),
            _ => throw new InvalidOperationException("The program has another native fact kind."),
        };
        var operation = fact.Operation;
        if (operation is null || operation.Actor is null || fact.Content is null || fact.Content.Schema != ProgramContent.Schema || fact.Content.MediaType != "application/json" || fact.TenantId != _tenantId || fact.ProgramId != Id || fact.NativeSequence != NativeSequence + 1 ||
            fact.FirstResponse is null || fact.OperationResult is null || fact.ChangeCount is < 0 or > 1 ||
            operation.Actor.Kind != "member" || !Uuid.TryParse(operation.Actor.Id, CultureInfo.InvariantCulture, out var member) || member == Uuid.Empty ||
            operation.AcceptedAt == default || !NativeOwnerHistory.IsOwnRoot(this, operation, kind, operation.OriginalExpectedRevision))
            throw Invalid();
        switch (fact)
        {
            case ProgramCreated created:
                if (_created || created.Revision != 1 || created.ChangeCount != 1 || operation.LogicalId != Id ||
                    operation.OriginalExpectedRevision is not null || created.ChangedAt != operation.AcceptedAt)
                    throw Invalid();
                _created = true;
                _content = created.Content;
                break;
            case ProgramRevised revised:
                if (!_created || operation.OriginalExpectedRevision != (ulong)Revision || revised.Revision != Revision + 1 ||
                    revised.ChangeCount != 1 || revised.ChangedAt != operation.AcceptedAt)
                    throw Invalid();
                _content = revised.Content;
                _lastChangeWasSelection = false;
                break;
            case ProgramCriteriaEditionSelected selected:
                if (!_created || operation.OriginalExpectedRevision is not { } expected || expected > long.MaxValue ||
                    selected.Content != _content || selected.EditionId == Uuid.Empty)
                    throw Invalid();
                var prepared = PrepareSelection((long)expected, selected.EditionId);
                if (!prepared.IsSuccess || selected.ChangeCount != (prepared.Value ? 1 : 0) ||
                    selected.Revision != Revision + selected.ChangeCount || selected.ChangedAt !=
                    (prepared.Value ? operation.AcceptedAt : _selection!.ChangedAt))
                    throw Invalid();
                if (selected.ChangeCount == 1)
                {
                    _selection = selected;
                    _lastChangeWasSelection = true;
                }
                break;
        }
        Acceptance.Observe((DomainEvent)fact);
        _revision = fact.Revision;
        _nativeSequence = fact.NativeSequence;
    }

    Result Stale() => Result.Failure(CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("program", Revision)).ToRequestError());
    static Result Failure(string message) => Result.Failure(new RequestError(RequestErrorKind.Conflict, message));
    static InvalidOperationException Invalid() => new("The program fact differs from its exact scope, revision or native transition.");
}
