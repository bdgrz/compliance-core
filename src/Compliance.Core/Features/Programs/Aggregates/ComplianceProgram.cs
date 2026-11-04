using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.History;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Versioning.Rules;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Programs.Commands;
using Bdgrz.Compliance.Features.Programs.Events;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Programs.Rules;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
namespace Bdgrz.Compliance.Features.Programs.Aggregates;

/// <summary>Current program authority and one selected native acceptance; all criteria history composes outside it.</summary>
public sealed class ComplianceProgram : Aggregate
{
    public const string Area = "programs";
    readonly Uuid _tenantId;
    bool _created;
    long _revision;
    bool _lastChangeWasSelection;
    ProgramCriteriaEditionSelected? _selection;
    Uuid? _creationEditionId;
    string _stage = "readiness";
    ImmutableContentRef? _content;
    ulong _nativeSequence;

    public ComplianceProgram(Uuid tenantId, Uuid programId, Uuid selectedLogicalId = default)
        : base(programId, new EventStreamAddress(tenantId.ToString(), Area, programId.ToString()))
    {
        _tenantId = tenantId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<ProgramCreated>(ApplyFact);
        On<ProgramCreatedV3>(ApplyFact);
        On<ProgramRenamed>(ApplyFact);
        On<ProgramTargetsRevised>(ApplyFact);
        On<ProgramStageChanged>(ApplyFact);
        On<ProgramRevised>(ApplyFact);
        On<ProgramCriteriaEditionSelected>(ApplyFact);
    }

    public Uuid TenantId => _tenantId;
    public bool IsCreated => _created;
    public long Revision => _revision;
    public Uuid? CriteriaEditionId => _selection?.EditionId ?? _creationEditionId;
    public string Stage => _stage;
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

    public Result CreateWithEdition(OperationProvenance operation, Uuid editionId, ImmutableContentRef content,
        ImmutableContentRef first, ImmutableContentRef result)
    {
        var guard = NativeCut.Require(this, Acceptance, operation);
        if (!guard.IsSuccess)
            return guard;
        var prepared = PrepareCreate();
        if (!prepared.IsSuccess)
            return prepared;
        if (editionId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "A program requires its published criteria edition."));
        Emit(new ProgramCreatedV3(_tenantId, Id, 1, editionId, content, operation.AcceptedAt,
            operation, first, result, NativeSequence + 1, 1));
        return Result.Success;
    }

    public Result Rename(OperationProvenance operation, string previousName, string name,
        ImmutableContentRef content, ImmutableContentRef first, ImmutableContentRef result)
    {
        var prepared = PrepareNamed(operation);
        if (!prepared.IsSuccess)
            return prepared;
        var changed = previousName != name;
        Emit(new ProgramRenamed(_tenantId, Id, Revision + (changed ? 1 : 0), previousName, name,
            _content!, content, operation.AcceptedAt, operation, first, result, NativeSequence + 1, changed ? 1 : 0));
        return Result.Success;
    }

    public Result ReviseTargets(OperationProvenance operation, ProgramPlan plan, ImmutableContentRef content,
        ImmutableContentRef first, ImmutableContentRef result)
    {
        var prepared = PrepareNamed(operation);
        if (!prepared.IsSuccess)
            return prepared;
        var changed = content != _content;
        Emit(new ProgramTargetsRevised(_tenantId, Id, Revision + (changed ? 1 : 0), plan,
            _content!, content, operation.AcceptedAt, operation, first, result, NativeSequence + 1, changed ? 1 : 0));
        return Result.Success;
    }

    public Result ChangeStage(OperationProvenance operation, string stage, ProgramStageSupport support,
        string rationale, ImmutableContentRef first, ImmutableContentRef result)
    {
        var prepared = PrepareNamed(operation);
        if (!prepared.IsSuccess)
            return prepared;
        var error = ProgramRules.StageError(_tenantId, Id, Stage, stage, support, rationale);
        if (error is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, error));
        Emit(new ProgramStageChanged(_tenantId, Id, Revision + 1, Stage, stage, support, rationale,
            _content!, operation.AcceptedAt, operation, first, result, NativeSequence + 1, 1));
        return Result.Success;
    }

    Result PrepareNamed(OperationProvenance operation)
    {
        var guard = NativeCut.Require(this, Acceptance, operation);
        if (!guard.IsSuccess)
            return guard;
        return operation.OriginalExpectedRevision is { } expected && expected <= long.MaxValue
            ? PrepareRevise((long)expected) : Failure("A named program change requires its original business revision.");
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
            ProgramCreatedV3 => nameof(CreateProgramV3),
            ProgramRenamed => nameof(RenameProgram),
            ProgramTargetsRevised => nameof(ReviseProgramTargets),
            ProgramStageChanged => nameof(ChangeProgramStage),
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
            case ProgramCreatedV3 created:
                if (_created || created.Revision != 1 || created.ChangeCount != 1 || created.EditionId == Uuid.Empty ||
                    operation.LogicalId != Id || operation.OriginalExpectedRevision is not null || created.ChangedAt != operation.AcceptedAt)
                    throw Invalid();
                _created = true;
                _content = created.Content;
                _creationEditionId = created.EditionId;
                break;
            case ProgramRenamed renamed:
                if (!_created || operation.OriginalExpectedRevision != (ulong)Revision || renamed.PreviousContent != _content ||
                    renamed.Revision != Revision + renamed.ChangeCount || renamed.ChangedAt != operation.AcceptedAt ||
                    string.IsNullOrWhiteSpace(renamed.PreviousName) || string.IsNullOrWhiteSpace(renamed.Name) ||
                    renamed.Name != renamed.Name.Trim() || renamed.ChangeCount != (renamed.Name == renamed.PreviousName ? 0 : 1) ||
                    (renamed.ChangeCount == 0) != (renamed.Content == _content))
                    throw Invalid();
                _content = renamed.Content;
                if (renamed.ChangeCount == 1)
                    _lastChangeWasSelection = false;
                break;
            case ProgramTargetsRevised targets:
                if (!_created || operation.OriginalExpectedRevision != (ulong)Revision || targets.PreviousContent != _content ||
                    targets.Revision != Revision + targets.ChangeCount || targets.ChangedAt != operation.AcceptedAt ||
                    targets.Plan is null || (targets.ChangeCount == 0) != (targets.Content == _content))
                    throw Invalid();
                _content = targets.Content;
                if (targets.ChangeCount == 1)
                    _lastChangeWasSelection = false;
                break;
            case ProgramStageChanged stage:
                if (!_created || operation.OriginalExpectedRevision != (ulong)Revision || stage.Content != _content ||
                    stage.Revision != Revision + 1 || stage.ChangeCount != 1 || stage.PreviousStage != Stage ||
                    stage.ChangedAt != operation.AcceptedAt || stage.Rationale != stage.Rationale?.Trim() ||
                    ProgramRules.StageError(_tenantId, Id, Stage, stage.Stage, stage.Support, stage.Rationale) is not null)
                    throw Invalid();
                _stage = stage.Stage;
                _lastChangeWasSelection = false;
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
