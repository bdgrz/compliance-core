using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Versioning.Rules;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Programs.Commands;
namespace Bdgrz.Compliance.Features.Programs.Rules;

public sealed class ProgramRules
{
    readonly Uuid _tenantId;
    bool _created;
    long _revision;
    Uuid? _criteriaEditionId;
    bool _lastChangeWasSelection;
    readonly List<(long Revision, Uuid EditionId, DateTimeOffset ChangedAt)> _criteriaEditionHistory = [];
    string? _initialName;
    ProgramPlan? _initialPlan;

    public bool IsCreated => _created;
    public long Revision => _revision;
    public Uuid? CriteriaEditionId => _criteriaEditionId;

    /// <summary>The exact criteria selection recorded at or before the requested time.</summary>
    public ProgramCriteriaEditionSelection? CriteriaSelectionAt(DateTimeOffset asOf)
    {
        var selected = _criteriaEditionHistory
            .Where(selection => selection.ChangedAt <= asOf)
            .OrderByDescending(static selection => selection.ChangedAt)
            .ThenByDescending(static selection => selection.Revision)
            .FirstOrDefault();
        return selected.Revision > 0
            ? new ProgramCriteriaEditionSelection(selected.Revision, selected.EditionId,
                selected.ChangedAt)
            : null;
    }

    public Uuid? CriteriaEditionAt(DateTimeOffset asOf) => CriteriaSelectionAt(asOf)?.EditionId;

    public ProgramRules(Uuid tenantId, Uuid programId)
    {
        _tenantId = tenantId;
        Id = programId;
    }

    public Uuid TenantId => _tenantId;
    public Uuid Id { get; }
    public IReadOnlyList<ProgramRuleChange> AppliedChanges => _applied;
    public IReadOnlyList<ProgramRuleChange> PendingChanges => _pending;
    readonly List<ProgramRuleChange> _applied = [];
    readonly List<ProgramRuleChange> _pending = [];
    public void ClearPendingChanges() => _pending.Clear();
    void Add(ProgramRuleChange change)
    {
        _applied.Add(change);
        _pending.Add(change);
        _revision = change.Revision;
        if (change.Kind == nameof(CreateProgram))
        {
            _created = true;
            _initialName = change.Content!.Name;
            _initialPlan = change.Content.Plan;
        }
        else if (change.Kind == nameof(SelectProgramCriteriaEdition))
        {
            _criteriaEditionId = change.EditionId;
            _criteriaEditionHistory.Add((change.Revision, change.EditionId!.Value, change.ChangedAt));
            _lastChangeWasSelection = true;
        }
        else
            _lastChangeWasSelection = false;
    }

    public CommandFailure? Create(string name, ProgramPlan plan, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset changedAt)
    {
        if (_created)
            return StringComparer.Ordinal.Equals(_initialName, name?.Trim()) &&
                   Equals(_initialPlan, plan)
                ? null
                : CommandFailure.StateConflict(
                    "The program already exists with different content.");
        var error = Validate(name, plan);
        if (error is not null)
            return CommandFailure.InvalidContent(error);
        Add(new ProgramRuleChange(nameof(CreateProgram), 1, new ProgramContent(name.Trim(), plan), null,
            ActorReference.ForMember(actorMemberId, actorDisplay), changedAt));
        return null;
    }

    public CommandFailure? Revise(long expectedRevision, string name, ProgramPlan plan,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset changedAt)
    {
        if (!_created)
            return CommandFailure.MissingRecord("The program was not found.");
        if (expectedRevision != _revision)
            return CommandFailure.ForVersion(
                VersionedRecordRules.StaleRevision("program", _revision));
        var error = Validate(name, plan);
        if (error is not null)
            return CommandFailure.InvalidContent(error);
        Add(new ProgramRuleChange(nameof(ReviseProgram), _revision + 1, new ProgramContent(name.Trim(), plan), null,
            ActorReference.ForMember(actorMemberId, actorDisplay), changedAt));
        return null;
    }

    public CommandFailure? SelectCriteriaEdition(long expectedRevision, Uuid editionId,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset changedAt)
    {
        if (!_created)
            return CommandFailure.MissingRecord("The program was not found.");
        if (editionId == Uuid.Empty)
            return CommandFailure.InvalidContent("A criteria edition is required.");
        // An exact retry either finds the edition already current at the expected revision,
        // or finds that its own selection is the only change since that revision.
        if (_criteriaEditionId == editionId &&
            (expectedRevision == _revision ||
             (_lastChangeWasSelection && expectedRevision + 1 == _revision)))
            return null;
        if (expectedRevision != _revision)
            return CommandFailure.ForVersion(
                VersionedRecordRules.StaleRevision("program", _revision));
        Add(new ProgramRuleChange(nameof(SelectProgramCriteriaEdition), _revision + 1, null, editionId,
            ActorReference.ForMember(actorMemberId, actorDisplay), changedAt));
        return null;
    }

    public static CommandFailure? Validate(ProgramContent? content)
    {
        var error = Validate(content?.Name!, content?.Plan);
        return error is null ? null : CommandFailure.InvalidContent(error);
    }

    static string? Validate(string name, ProgramPlan? plan)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "A program requires a name.";
        if (plan is null)
            return "A program requires a plan.";
        if (plan.TargetReadinessDate > plan.TargetTypeIAsOfDate)
            return "The Type I target date must follow the readiness target date.";
        if (plan.TargetTypeIIStartDate > plan.TargetTypeIIEndDate)
            return "The Type II target end date must follow its start date.";
        if (plan.TargetTypeIAsOfDate > plan.TargetTypeIIStartDate)
            return "The Type II target period must follow the Type I target date.";
        return null;
    }
}
