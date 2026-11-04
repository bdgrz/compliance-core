using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;

/// <summary>Records one terminal owning-context outcome for every entry of one applied template version.</summary>
public sealed class TemplateVersionApplication : Aggregate
{
    public const string Area = "template-version-applications";
    readonly Uuid _tenantId;
    readonly Uuid _programId;
    readonly Uuid _templateId;
    readonly long _version;
    readonly HashSet<string> _entries = new(StringComparer.Ordinal);
    readonly Dictionary<string, TemplateEntryOutcome> _outcomes = new(StringComparer.Ordinal);
    readonly Dictionary<string, TemplateEntryProposalBase> _proposalBases = new(StringComparer.Ordinal);

    public TemplateVersionApplication(Uuid tenantId, Uuid programId, Uuid templateId, long version)
        : base(Identity(tenantId, programId, templateId, version),
            new EventStreamAddress(tenantId.ToString(), Area, Identity(tenantId, programId, templateId, version).ToString()))
    {
        (_tenantId, _programId, _templateId, _version) = (tenantId, programId, templateId, version);
        On<TemplateVersionApplicationOpened>(Apply);
        On<TemplateEntryOutcomeRecorded>(Apply);
        On<TemplateEntryProposalPrepared>(Apply);
        On<TemplateVersionApplicationCompleted>(Apply);
    }

    public Uuid ApplicationEventId { get; private set; }
    public Uuid ApplicationCommandId { get; private set; }
    public bool IsOpened => ApplicationEventId != Uuid.Empty;
    public TemplateEntryOutcome? OutcomeFor(string entryKey) => _outcomes.GetValueOrDefault(entryKey);
    public TemplateEntryProposalBase? PreparedProposalFor(string entryKey) => _proposalBases.GetValueOrDefault(entryKey);

    public Result PrepareProposal(TemplateEntryProposalBase proposal)
    {
        if (!IsOpened)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The template version application is not open yet.", isTransient: true));
        if (!ValidProposal(proposal) || !_entries.Contains(proposal.EntryKey))
            return Invalid("Proposal preparation requires a listed entry and one exact approved destination base.");
        if (_proposalBases.TryGetValue(proposal.EntryKey, out var prior))
            return prior == proposal ? Result.Success : Conflict("The entry's approved proposal base is already frozen.");
        if (IsCompleted || _outcomes.ContainsKey(proposal.EntryKey))
            return Conflict("A terminal entry cannot select another proposal base.");
        RaiseEvent(new TemplateEntryProposalPrepared(_tenantId, _programId, _templateId, _version,
            ApplicationEventId, proposal));
        return Result.Success;
    }

    public bool IsCompleted { get; private set; }
    public TemplateApplicationOutcomeCounts Counts => new(
        _outcomes.Values.Count(item => item.Outcome == "created"),
        _outcomes.Values.Count(item => item.Outcome == "proposed"),
        _outcomes.Values.Count(item => item.Outcome == "refused"));

    public static Uuid Identity(Uuid tenantId, Uuid programId, Uuid templateId, long version)
    {
        if (tenantId == Uuid.Empty || programId == Uuid.Empty || templateId == Uuid.Empty)
            throw new ArgumentException("A template version application requires its tenant, program and template identities.");
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);
        return Uuid.CreateVersion5(tenantId,
            $"template-version-application:{programId}:{templateId}:{version.ToString(CultureInfo.InvariantCulture)}");
    }

    public Result Open(Uuid applicationEventId, IReadOnlyList<string> entryKeys, Uuid applicationCommandId = default)
    {
        if (applicationEventId == Uuid.Empty || !ValidEntries(entryKeys))
            return Invalid("Opening requires an exact application source and unique bounded entry keys.");
        if (IsOpened)
            return ApplicationEventId == applicationEventId && ApplicationCommandId == applicationCommandId && _entries.SetEquals(entryKeys)
                ? Result.Success : Conflict("The version application cannot replace its original source or entry set.");
        RaiseEvent(new TemplateVersionApplicationOpened(_tenantId, _programId, _templateId, _version,
            applicationEventId, entryKeys.Order(StringComparer.Ordinal).ToArray(), applicationCommandId));
        CompleteIfReady();
        return Result.Success;
    }

    public Result Record(TemplateEntryOutcome outcome)
    {
        if (!IsOpened)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The template version application is not open yet.", isTransient: true));
        if (!ValidOutcome(outcome) || !_entries.Contains(outcome.EntryKey))
            return Invalid("An outcome must name a listed entry and one created, proposed or reasoned refused result.");
        if (_outcomes.TryGetValue(outcome.EntryKey, out var prior))
            return prior == outcome ? Result.Success : Conflict("An entry already has another terminal outcome.");
        if (IsCompleted)
            return Conflict("A completed version application accepts no further outcomes.");
        RaiseEvent(new TemplateEntryOutcomeRecorded(_tenantId, _programId, _templateId, _version,
            ApplicationEventId, outcome));
        CompleteIfReady();
        return Result.Success;
    }

    void CompleteIfReady()
    {
        if (!IsCompleted && _outcomes.Count == _entries.Count)
            RaiseEvent(new TemplateVersionApplicationCompleted(_tenantId, _programId, _templateId, _version,
                ApplicationEventId, Counts, ApplicationCommandId));
    }

    void Apply(TemplateVersionApplicationOpened fact)
    {
        RequireScope(fact.TenantId, fact.ProgramId, fact.TemplateId, fact.TemplateVersion);
        if (IsOpened || fact.ApplicationEventId == Uuid.Empty || !ValidEntries(fact.EntryKeys))
            throw new InvalidOperationException("A version application opens once from an exact source and unique entries.");
        ApplicationEventId = fact.ApplicationEventId;
        ApplicationCommandId = fact.ApplicationCommandId;
        _entries.UnionWith(fact.EntryKeys);
    }

    void Apply(TemplateEntryProposalPrepared fact)
    {
        RequireScope(fact.TenantId, fact.ProgramId, fact.TemplateId, fact.TemplateVersion);
        if (!IsOpened || IsCompleted || fact.ApplicationEventId != ApplicationEventId ||
            !ValidProposal(fact.ProposalBase) || !_entries.Contains(fact.ProposalBase.EntryKey) ||
            _outcomes.ContainsKey(fact.ProposalBase.EntryKey) || !_proposalBases.TryAdd(fact.ProposalBase.EntryKey, fact.ProposalBase))
            throw new InvalidOperationException("A listed nonterminal entry freezes one exact approved destination base.");
    }

    static bool ValidProposal(TemplateEntryProposalBase? proposal) => proposal is not null && ValidKey(proposal.EntryKey) &&
        proposal.RecordKind is "control" or "policy" or "risk" && proposal.RecordId != Uuid.Empty &&
        (proposal.ApprovedVersionId is { } version && version != Uuid.Empty && proposal.ApprovedRevision is null ||
         proposal.ApprovedVersionId is null && proposal.ApprovedRevision is > 0);

    void Apply(TemplateEntryOutcomeRecorded fact)
    {
        RequireScope(fact.TenantId, fact.ProgramId, fact.TemplateId, fact.TemplateVersion);
        if (!IsOpened || IsCompleted || fact.ApplicationEventId != ApplicationEventId ||
            !ValidOutcome(fact.Outcome) || !_entries.Contains(fact.Outcome.EntryKey) ||
            !_outcomes.TryAdd(fact.Outcome.EntryKey, fact.Outcome))
            throw new InvalidOperationException("A listed entry records exactly one valid terminal outcome.");
    }

    void Apply(TemplateVersionApplicationCompleted fact)
    {
        RequireScope(fact.TenantId, fact.ProgramId, fact.TemplateId, fact.TemplateVersion);
        if (!IsOpened || IsCompleted || fact.ApplicationEventId != ApplicationEventId || fact.ApplicationCommandId != ApplicationCommandId ||
            _outcomes.Count != _entries.Count || fact.Counts != Counts)
            throw new InvalidOperationException("Completion requires one outcome per entry and its exact outcome totals.");
        IsCompleted = true;
    }

    void RequireScope(Uuid tenant, Uuid program, Uuid template, long version)
    {
        if (tenant != _tenantId || program != _programId || template != _templateId || version != _version)
            throw new InvalidOperationException("The version application fact belongs to another tenant, program, template or version.");
    }

    static bool ValidEntries(IReadOnlyList<string>? entries) => entries is { Count: <= 100 } &&
        entries.All(ValidKey) && entries.Distinct(StringComparer.Ordinal).Count() == entries.Count;

    static bool ValidKey(string? key) => key is { Length: > 0 and <= 200 } && key == key.Trim() &&
        key.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');

    static bool ValidOutcome(TemplateEntryOutcome? outcome) => outcome is not null && ValidKey(outcome.EntryKey) &&
        (outcome.Outcome is "created" or "proposed"
            ? outcome.RecordId is { } record && record != Uuid.Empty && outcome.RefusalReason is null
            : outcome.Outcome == "refused" && outcome.RecordId is null &&
              outcome.RefusalReason is { Length: > 0 and <= 2000 } reason && reason == reason.Trim());

    static Result Invalid(string message) => Result.Failure(new RequestError(RequestErrorKind.Validation, message));
    static Result Conflict(string message) => Result.Failure(new RequestError(RequestErrorKind.Conflict, message));
}
