using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AccessControl.Aggregates;

/// <summary>Exact available contributor identities, live transitions and one selected historical episode; full chronology is external.</summary>
public sealed class TenantManagerGuard : Aggregate
{
    public const string Suspended = "suspended";
    public const string Deprovisioned = "deprovisioned";
    public const string RemovedFromAdministrators = "administrators_team_removed";
    public const string Registered = "registered";
    readonly Dictionary<Uuid, TenantManagerEpisodeAuthority> _authority = [];
    readonly Dictionary<Uuid, PendingTransition> _pending = [];
    Uuid _selectedEpisode;
    bool _hasFacts;
    bool _emitting;
    GuardBaseline? _seedInput;
    public TenantManagerGuard(Uuid tenantId, Uuid selectedCommandId = default, GuardBaseline? baseline = null,
        Uuid selectedEpisode = default, Uuid selectedRoot = default)
        : base(tenantId, new EventStreamAddress(tenantId.ToString(), "rbac-manager-guard", tenantId.ToString()))
    {
        TenantId = tenantId;
        SelectedRoot = selectedRoot == Uuid.Empty ? selectedCommandId : selectedRoot;
        _selectedEpisode = selectedEpisode;
        _seedInput = baseline;
        Acceptance = new NativeAcceptance(selectedCommandId);
        foreach (var seed in baseline?.Contributions ?? [])
        {
            var value = new TenantManagerEpisodeAuthority(seed.MemberEpisodeId, seed.MemberId, true, true,
                seed.AssignmentGeneration, seed.Reasons.Order(StringComparer.Ordinal).ToArray(), seed.Reasons.Count == 0, []);
            if (value.Available || seed.MemberEpisodeId == selectedEpisode)
                _authority[seed.MemberEpisodeId] = value;
        }
        On<TenantManagerWithdrawalAccepted>(ApplyFact);
        On<TenantManagerWithdrawalCompleted>(ApplyFact);
        On<TenantManagerRestorationAccepted>(ApplyFact);
        On<TenantManagerRestorationCompleted>(ApplyFact);
        On<TenantManagerSourceMutationAdmitted>(ApplyFact);
        On<TenantManagerRestored>(ApplyFact);
    }
    public static RequestError StaleConflict { get; } = new(RequestErrorKind.Conflict, "Another tenant manager change committed first.", true);
    public static RequestError LastAdministratorConflict { get; } = new(RequestErrorKind.Conflict, "This change would leave the tenant without an active administrator.", false);
    public Uuid TenantId { get; }
    public NativeAcceptance Acceptance { get; }
    public ImmutableContentRef? BaselineRef { get; private set; }
    public long NextGeneration => (long)Version + 1;
    public bool IsEmpty => !_hasFacts;
    public int AvailableCount => _authority.Values.Count(value => value.Available);
    public int PendingCount => _pending.Count;
    internal GuardBaseline? TakeSeed()
    {
        var value = _seedInput;
        _seedInput = null;
        return value;
    }
    internal Uuid SelectedEpisode => _selectedEpisode;
    internal Uuid SelectedRoot { get; }
    internal IReadOnlyDictionary<Uuid, TenantManagerEpisodeAuthority> Authority => _authority;
    internal IReadOnlyDictionary<Uuid, PendingTransition> Pending => _pending;
    internal NativeReplayCommitment ReplayCut { get; } = new();
    public bool HasContribution(Uuid episode) => _authority.TryGetValue(episode, out var value) && value.HasContribution;
    public bool IsAvailable(Uuid episode) => _authority.TryGetValue(episode, out var value) && value.Available;
    public IReadOnlyCollection<string> ReasonsFor(Uuid episode) => _authority.GetValueOrDefault(episode)?.Reasons.ToArray() ?? [];
    public bool IsRegistrationPending(Uuid episode) => _pending.Values.Any(value => value.IsRegistration && value.Episode == episode);
    public bool SuspensionAdmitted(Uuid episode) => ReasonsFor(episode).Contains(Suspended) || _authority.GetValueOrDefault(episode)?.NeutralReasons.Contains(Suspended) == true;
    public bool DeprovisionAdmitted(Uuid episode) => ReasonsFor(episode).Contains(Deprovisioned) || _authority.GetValueOrDefault(episode)?.NeutralReasons.Contains(Deprovisioned) == true;
    public IReadOnlyList<PendingTransition> PendingFor(Uuid episode) => _pending.Values.Where(value => value.Episode == episode).ToArray();
    public bool IsPending(Uuid rootOperationId) => _pending.ContainsKey(rootOperationId);
    public long? PendingGeneration(Uuid rootOperationId) => _pending.GetValueOrDefault(rootOperationId)?.Generation;
    public GateDecision GateRestoration(Uuid rootOperationId) => CurrentRules().GateRestoration(rootOperationId);
    public Result<AdmissionPlan> Plan(string commandKind, Uuid episode) => HasSelectedEpisode(episode)
        ? CurrentRules().Plan(commandKind, episode)
        : Result<AdmissionPlan>.Failure(new RequestError(RequestErrorKind.Conflict, "The exact historical membership episode must be selected before administrator admission.", true));

    public Result Admit(AdmissionPlan plan, Uuid episode, Uuid memberId, OperationProvenance operation, EventStreamAddress owner,
        long revision, ImmutableContentRef intent, ImmutableContentRef firstResponse, ImmutableContentRef? baseline) =>
        Execute(operation, model => model.Admit(plan, episode, memberId, operation, owner, revision, intent, firstResponse, baseline));
    public Result CompleteWithdrawal(OperationProvenance operation, Uuid rootOperationId, EventStreamAddress owner,
        long revision, ImmutableContentRef intent, ImmutableContentRef? proofs, ImmutableContentRef? result) =>
        Execute(operation, model => model.CompleteWithdrawal(operation, rootOperationId, owner, revision, intent, proofs, result));
    public Result CompleteRestoration(OperationProvenance operation, Uuid rootOperationId, Uuid memberId, bool sourceEligible,
        EventStreamAddress owner, long revision, ImmutableContentRef intent, ImmutableContentRef? proofs, ImmutableContentRef? result) =>
        Execute(operation, model => model.CompleteRestoration(operation, rootOperationId, memberId, sourceEligible, owner, revision, intent, proofs, result));

    bool HasSelectedEpisode(Uuid episode) => episode == _selectedEpisode || _authority.ContainsKey(episode);
    TenantManagerGuardRules CurrentRules() => TenantManagerGuardRules.FromCurrent(TenantId, Version, _authority.Values, _pending, _hasFacts, BaselineRef);
    Result Execute(OperationProvenance operation, Func<TenantManagerGuardRules, Result> command)
    {
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        var model = CurrentRules();
        var result = command(model);
        if (!result.IsSuccess)
            return result;
        _emitting = true;
        try
        {
            foreach (var fact in model.Emitted)
                RaiseEvent(fact);
        }
        finally
        {
            _emitting = false;
        }
        return Result.Success;
    }
    void ApplyFact<T>(T fact) where T : DomainEvent, ITenantManagerAuthorityFact
    {
        var value = fact.CurrentAuthority ?? throw new InvalidOperationException("A native guard fact requires its bounded affected-episode authority.");
        if (fact is TenantManagerRestored restored && (restored.TenantId != TenantId ||
            restored.MemberId != value.MemberId || !_pending.TryGetValue(restored.Operation.RootOperationId, out var restoration) ||
            restoration.IsWithdrawal || (restoration.Reason ?? Registered) != restored.Reason))
            throw new InvalidOperationException("A restored administrator must match its tenant, member, and pending restoration.");
        if (!Uuid.TryParse(fact.Operation.Episode, System.Globalization.CultureInfo.InvariantCulture, out var episode) || episode != value.EpisodeId ||
            value.MemberId == Uuid.Empty || fact.Operation.Owner != Stream || value.Reasons is null || value.NeutralReasons is null ||
            value.AssignmentGeneration < 0 || value.Available && !value.HasContribution ||
            value.Reasons.Distinct(StringComparer.Ordinal).Count() != value.Reasons.Count ||
            value.NeutralReasons.Distinct(StringComparer.Ordinal).Count() != value.NeutralReasons.Count ||
            value.Reasons.Any(reason => reason is not (Suspended or Deprovisioned or RemovedFromAdministrators)) ||
            value.NeutralReasons.Any(reason => reason is not (Suspended or Deprovisioned)))
            throw new InvalidOperationException("The native guard authority differs from its actual episode and emitting owner.");
        if (fact is not TenantManagerRestored)
        {
            Acceptance.Observe(fact);
            if (fact.Operation.LogicalId == Acceptance.SelectedLogicalId)
                _selectedEpisode = episode;
            if (!_hasFacts)
            {
                _hasFacts = true;
                BaselineRef = ((ITenantManagerGuardFact)fact).SourceProofs;
            }
        }
        switch (fact)
        {
            case TenantManagerWithdrawalAccepted accepted:
                var reason = AdministratorCommandKinds.WithdrawalReason(accepted.Operation.CommandKind)
                    ?? throw new InvalidOperationException("An accepted withdrawal requires a withdrawal command.");
                _pending[accepted.Operation.RootOperationId] = new PendingTransition(true, episode, reason, accepted.Generation, false);
                foreach (var pair in _pending.Where(pair => !pair.Value.IsWithdrawal && pair.Value.Episode == episode && pair.Value.Reason == reason && pair.Value.Generation < accepted.Generation).ToArray())
                    _pending[pair.Key] = pair.Value with { Superseded = true };
                break;
            case TenantManagerRestorationAccepted accepted:
                var registration = accepted.Operation.CommandKind != AdministratorCommandKinds.Reinstate && !value.IsRegistered;
                _pending[accepted.Operation.RootOperationId] = new PendingTransition(false, episode,
                    accepted.Operation.CommandKind == AdministratorCommandKinds.Reinstate ? Suspended : registration ? null : RemovedFromAdministrators,
                    accepted.Generation, registration);
                break;
            case TenantManagerWithdrawalCompleted or TenantManagerRestorationCompleted:
                _pending.Remove(fact.Operation.RootOperationId);
                break;
        }
        _authority[episode] = value;
        foreach (var pair in _authority.Where(pair => !pair.Value.Available && pair.Key != _selectedEpisode && !_pending.Values.Any(pending => pending.Episode == pair.Key)).ToArray())
            _authority.Remove(pair.Key);
    }
    protected override void Apply(DomainEvent ev)
    {
        if (!ev.Metadata.IsAudit)
            ReplayCut.Observe(ev, _emitting);
        base.Apply(ev);
    }
}
