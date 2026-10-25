using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The tenant conflict owner for changes that can remove an administrator (#432, #1022). It keeps the compact set
///     of administrator contributions, keyed by member episode, and the active withdrawal and restoration operations.
///     Every source change that can remove availability appends a guard fact first, and availability only returns
///     after the exact source restoration and current eligibility are proven; a directory boolean never proves
///     another administrator. At least one other current, unwithdrawn, eligible contribution must remain for a
///     withdrawal of an available contribution to be accepted.
/// </summary>
sealed class TenantManagerGuardRules
{
    /// <summary>Withheld while the member episode is suspended.</summary>
    public const string Suspended = "suspended";

    /// <summary>Withheld once the member episode is deprovisioned.</summary>
    public const string Deprovisioned = "deprovisioned";

    /// <summary>Withheld while the administrators-team assignment is removed.</summary>
    public const string RemovedFromAdministrators = "administrators_team_removed";

    /// <summary>The restoration reason recorded when a first registration restores availability.</summary>
    public const string Registered = "registered";

    readonly Uuid _tenantId;
    readonly Dictionary<Uuid, Contribution> _contributions = [];
    readonly Dictionary<Uuid, PendingTransition> _pending = [];
    readonly Dictionary<Uuid, HashSet<string>> _neutral = [];
    readonly Dictionary<Uuid, Uuid> _members = [];
    readonly Dictionary<Type, Action<DomainEvent>> _handlers = [];
    readonly List<DomainEvent> _emitted = [];
    public EventStreamAddress Stream { get; }
    public ulong Version { get; private set; }
    public IReadOnlyList<DomainEvent> Emitted => _emitted;
    public IReadOnlyDictionary<Uuid, PendingTransition> Pending => _pending;
    bool _hasFacts;

    /// <summary>The error a lost race on the guard reports: another guard fact committed between plan and append.</summary>
    public static RequestError StaleConflict => TenantManagerGuard.StaleConflict;
    public static RequestError LastAdministratorConflict => TenantManagerGuard.LastAdministratorConflict;

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The baseline content the first guard fact referenced, once the stream is not empty.</summary>
    public ImmutableContentRef? BaselineRef { get; private set; }

    /// <summary>The generation the next guard fact will carry.</summary>
    public long NextGeneration => (long)Version + 1;

    /// <summary>True until the first guard fact: the contribution set must then come from a source cut.</summary>
    public bool IsEmpty => !_hasFacts;

    /// <summary>The number of contributions that are currently available.</summary>
    public int AvailableCount => _contributions.Values.Count(static contribution => contribution.Available);

    /// <summary>The number of operations accepted and not yet completed.</summary>
    public int PendingCount => _pending.Count;

    /// <summary>Creates the guard of one tenant; a baseline seeds the contributions of a tenant whose stream predates it.</summary>
    public TenantManagerGuardRules(Uuid tenantId, GuardBaseline? baseline = null)
    {
        Stream = new TenantManagerGuard(tenantId).Stream;
        _tenantId = tenantId;
        Acceptance = new NativeAcceptance(Uuid.Empty);
        foreach (var seed in baseline?.Contributions ?? [])
        {
            _contributions[seed.MemberEpisodeId] = new Contribution(true, seed.AssignmentGeneration, seed.Reasons);
            _members[seed.MemberEpisodeId] = seed.MemberId;
        }

        On<TenantManagerWithdrawalAccepted>(accepted =>
        {
            Observe(accepted, accepted.SourceProofs);
            var episode = EpisodeOf(accepted.Operation);
            var reason = AdministratorCommandKinds.WithdrawalReason(accepted.Operation.CommandKind)
                ?? throw new InvalidOperationException("A withdrawal must come from a withdrawing command.");
            var contribution = ContributionOf(episode);
            contribution.Reasons.Add(reason);
            contribution.Available = false;
            _pending[accepted.Operation.RootOperationId] = new PendingTransition(true, episode, reason, accepted.Generation, false);
            foreach (var pair in _pending.Where(pair => !pair.Value.IsWithdrawal && pair.Value.Episode == episode &&
                         pair.Value.Reason == reason && pair.Value.Generation < accepted.Generation).ToArray())
                _pending[pair.Key] = pair.Value with { Superseded = true };
        });
        On<TenantManagerWithdrawalCompleted>(completed =>
        {
            Observe(completed, completed.SourceProofs);
            _pending.Remove(completed.Operation.RootOperationId);
        });
        On<TenantManagerRestorationAccepted>(accepted =>
        {
            Observe(accepted, accepted.SourceProofs);
            var episode = EpisodeOf(accepted.Operation);
            if (accepted.Operation.CommandKind == AdministratorCommandKinds.Reinstate)
            {
                _pending[accepted.Operation.RootOperationId] = new PendingTransition(false, episode, Suspended, accepted.Generation, false);
                return;
            }

            if (_contributions.ContainsKey(episode))
            {
                _pending[accepted.Operation.RootOperationId] = new PendingTransition(false, episode,
                    RemovedFromAdministrators, accepted.Generation, false);
                return;
            }

            // A first registration observes every neutral mutation admitted earlier for this episode.
            _contributions[episode] = new Contribution(false, 0, _neutral.GetValueOrDefault(episode) ?? []);
            _pending[accepted.Operation.RootOperationId] = new PendingTransition(false, episode, null, accepted.Generation, true);
        });
        On<TenantManagerRestorationCompleted>(completed =>
        {
            Observe(completed, completed.SourceProofs);
            if (!_pending.Remove(completed.Operation.RootOperationId, out var pending) ||
                !_contributions.TryGetValue(pending.Episode, out var contribution))
                return;
            if (pending.IsRegistration)
            {
                contribution.IsRegistered = true;
                contribution.AssignmentGeneration++;
            }
            else if (pending.Reason is { } reason)
            {
                contribution.Reasons.Remove(reason);
                if (reason == RemovedFromAdministrators)
                    contribution.AssignmentGeneration++;
            }
        });
        On<TenantManagerRestored>(restored =>
        {
            if (_contributions.TryGetValue(EpisodeOf(restored.Operation), out var contribution))
                contribution.Available = true;
        });
        On<TenantManagerSourceMutationAdmitted>(admitted =>
        {
            Observe(admitted, admitted.SourceProofs);
            var episode = EpisodeOf(admitted.Operation);
            if (!_neutral.TryGetValue(episode, out var reasons))
                _neutral[episode] = reasons = new HashSet<string>(StringComparer.Ordinal);
            switch (admitted.Operation.CommandKind)
            {
                case AdministratorCommandKinds.Suspend:
                    reasons.Add(Suspended);
                    break;
                case AdministratorCommandKinds.Deprovision:
                    reasons.Add(Deprovisioned);
                    break;
                case AdministratorCommandKinds.Reinstate:
                    reasons.Remove(Suspended);
                    break;
            }
        });
    }

    /// <summary>True when the guard holds a contribution for the member episode.</summary>
    public bool HasContribution(Uuid episode) => _contributions.ContainsKey(episode);

    /// <summary>True when the member episode counts as an available administrator.</summary>
    public bool IsAvailable(Uuid episode) => _contributions.TryGetValue(episode, out var contribution) && contribution.Available;

    /// <summary>The reasons currently withholding the member episode's availability.</summary>
    public IReadOnlyCollection<string> ReasonsFor(Uuid episode) =>
        _contributions.TryGetValue(episode, out var contribution) ? [.. contribution.Reasons] : [];

    /// <summary>True when the episode's registration has been accepted and its assignment not yet proven.</summary>
    public bool IsRegistrationPending(Uuid episode) =>
        _pending.Values.Any(pending => pending.IsRegistration && pending.Episode == episode);

    /// <summary>True when a suspension of the episode is admitted, applied or not.</summary>
    public bool SuspensionAdmitted(Uuid episode) => ReasonsFor(episode).Contains(Suspended) ||
        _neutral.GetValueOrDefault(episode)?.Contains(Suspended) == true;

    /// <summary>True when a deprovisioning of the episode is admitted, applied or not.</summary>
    public bool DeprovisionAdmitted(Uuid episode) => ReasonsFor(episode).Contains(Deprovisioned) ||
        _neutral.GetValueOrDefault(episode)?.Contains(Deprovisioned) == true;

    /// <summary>The unfinished operations of one member episode.</summary>
    public IReadOnlyList<PendingTransition> PendingFor(Uuid episode) =>
        [.. _pending.Values.Where(pending => pending.Episode == episode)];

    /// <summary>True when an operation with this root identity is accepted and unfinished.</summary>
    public bool IsPending(Uuid rootOperationId) => _pending.ContainsKey(rootOperationId);

    /// <summary>The guard generation that accepted the unfinished operation, or null when none is pending.</summary>
    public long? PendingGeneration(Uuid rootOperationId) =>
        _pending.TryGetValue(rootOperationId, out var pending) ? pending.Generation : null;

    /// <summary>
    ///     Chooses the branch for a command on a member episode from current guard state without raising anything.
    ///     Admission is route-determined by the caller; this only decides which guard fact the route appends.
    /// </summary>
    public Result<AdmissionPlan> Plan(string commandKind, Uuid episode)
    {
        ArgumentNullException.ThrowIfNull(commandKind);
        if (episode == Uuid.Empty)
            return Result<AdmissionPlan>.Failure(new RequestError(RequestErrorKind.Validation,
                "A member episode is required."));
        var generation = NextGeneration;
        _contributions.TryGetValue(episode, out var contribution);
        switch (commandKind)
        {
            case AdministratorCommandKinds.Suspend or AdministratorCommandKinds.Deprovision
                or AdministratorCommandKinds.Remove:
                {
                    var reason = AdministratorCommandKinds.WithdrawalReason(commandKind)!;
                    var kind = commandKind switch
                    {
                        AdministratorCommandKinds.Suspend => AdministratorTransitionKind.SuspendMember,
                        AdministratorCommandKinds.Deprovision => AdministratorTransitionKind.DeprovisionMember,
                        _ => AdministratorTransitionKind.RemoveAdministratorsAssignment,
                    };
                    if (contribution is null)
                        return Result<AdmissionPlan>.Success(new AdmissionPlan(
                            commandKind == AdministratorCommandKinds.Remove
                                ? AdministratorTransitionKind.NeutralAssignmentMutation
                                : AdministratorTransitionKind.NeutralMemberMutation, null, generation));
                    if (contribution.Reasons.Contains(reason))
                        return Result<AdmissionPlan>.Failure(new RequestError(RequestErrorKind.Conflict,
                            "This change is already admitted for the member."));
                    if (contribution.Available && !OtherAvailable(episode))
                        return Result<AdmissionPlan>.Failure(LastAdministratorConflict);
                    return Result<AdmissionPlan>.Success(new AdmissionPlan(kind, reason, generation));
                }
            case AdministratorCommandKinds.Reinstate:
                if (contribution is null)
                    return Result<AdmissionPlan>.Success(new AdmissionPlan(
                        AdministratorTransitionKind.NeutralMemberMutation, null, generation));
                return contribution.Reasons.Contains(Suspended)
                    ? Result<AdmissionPlan>.Success(new AdmissionPlan(AdministratorTransitionKind.ReinstateMember,
                        Suspended, generation))
                    : Result<AdmissionPlan>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The member is not suspended."));
            case AdministratorCommandKinds.Assign:
                if (contribution is null)
                    return Result<AdmissionPlan>.Success(new AdmissionPlan(
                        AdministratorTransitionKind.RegisterAdministratorsAssignment, null, generation));
                if (!contribution.IsRegistered)
                    return Result<AdmissionPlan>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "An administrator registration for this member is already pending.", true));
                return contribution.Reasons.Contains(RemovedFromAdministrators)
                    ? Result<AdmissionPlan>.Success(new AdmissionPlan(
                        AdministratorTransitionKind.RestoreAdministratorsAssignment, RemovedFromAdministrators,
                        generation))
                    : Result<AdmissionPlan>.Failure(TeamMember.AlreadyAssignedConflict);
            default:
                return Result<AdmissionPlan>.Failure(new RequestError(RequestErrorKind.Validation,
                    "The command does not take part in administrator admission."));
        }
    }

    /// <summary>
    ///     Accepts the planned admission as the first native fact of <paramref name="operation" />. The plan is
    ///     recomputed against current state first, so a plan made on an older tail is refused as stale.
    /// </summary>
    public Result Admit(AdmissionPlan plan, Uuid episode, Uuid memberId, OperationProvenance operation, EventStreamAddress owner,
        long revision, ImmutableContentRef intent, ImmutableContentRef firstResponse, ImmutableContentRef? baseline)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(firstResponse);
        if (operation.LogicalId == Uuid.Empty || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A guard admission requires its own logical operation."));
        if (!_hasFacts && baseline is null)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The first guard fact must reference its authoritative source cut."));
        RegisterMember(episode, memberId);
        var current = Plan(operation.CommandKind, episode);
        if (!current.IsSuccess)
            return Result.Failure(current.Error!);
        if (current.Value != plan)
            return Result.Failure(StaleConflict);
        var proofs = _hasFacts ? null : baseline;
        var generation = plan.Generation;
        if (plan.IsWithdrawal)
            RaiseEvent(new TenantManagerWithdrawalAccepted(owner, revision, generation, intent, proofs, operation,
                firstResponse, null));
        else if (plan.IsRestoration)
            RaiseEvent(new TenantManagerRestorationAccepted(owner, revision, generation, intent, proofs, operation,
                firstResponse, null));
        else
            RaiseEvent(new TenantManagerSourceMutationAdmitted(owner, revision, generation, intent, proofs, operation,
                firstResponse, null));
        return Result.Success;
    }

    /// <summary>Whether a restoration receiver may apply its source change now, must wait, or is superseded.</summary>
    public GateDecision GateRestoration(Uuid rootOperationId)
    {
        if (!_pending.TryGetValue(rootOperationId, out var restoration) || restoration.IsWithdrawal)
            return GateDecision.Unknown;
        if (restoration.Reason is not { } reason)
            return GateDecision.Proceed;
        if (_pending.Values.Any(other => other.IsWithdrawal && other.Episode == restoration.Episode &&
                                         other.Reason == reason && other.Generation < restoration.Generation))
            return GateDecision.Wait;
        return restoration.Superseded ? GateDecision.Superseded : GateDecision.Proceed;
    }

    /// <summary>Settles the withdrawal once its exact source fact was applied; the reason stays withheld.</summary>
    public Result CompleteWithdrawal(OperationProvenance operation, Uuid rootOperationId, EventStreamAddress owner,
        long revision, ImmutableContentRef intent, ImmutableContentRef? proofs, ImmutableContentRef? result)
    {
        var accepted = CheckCompletion(operation, rootOperationId, true);
        if (!accepted.IsSuccess)
            return accepted;
        RaiseEvent(new TenantManagerWithdrawalCompleted(owner, revision, NextGeneration, intent, proofs, operation,
            null, result));
        return Result.Success;
    }

    /// <summary>
    ///     Settles the restoration once its exact source fact was applied. Availability returns, as
    ///     <see cref="TenantManagerRestored" />, only when no other reason remains and current source eligibility was
    ///     proven; otherwise the restoration completes without making the member available.
    /// </summary>
    public Result CompleteRestoration(OperationProvenance operation, Uuid rootOperationId, Uuid memberId,
        bool sourceEligible, EventStreamAddress owner, long revision, ImmutableContentRef intent,
        ImmutableContentRef? proofs, ImmutableContentRef? result)
    {
        var accepted = CheckCompletion(operation, rootOperationId, false);
        if (!accepted.IsSuccess)
            return accepted;
        var pending = _pending[rootOperationId];
        RegisterMember(pending.Episode, memberId);
        var contribution = _contributions[pending.Episode];
        var remaining = contribution.Reasons.Where(reason => reason != pending.Reason).Any();
        if (sourceEligible && !remaining && (contribution.IsRegistered || pending.IsRegistration))
            RaiseEvent(new TenantManagerRestored(_tenantId, memberId, pending.Reason ?? Registered, operation, intent,
                null, result));
        RaiseEvent(new TenantManagerRestorationCompleted(owner, revision, NextGeneration, intent, proofs, operation,
            null, result));
        return Result.Success;
    }

    Result CheckCompletion(OperationProvenance operation, Uuid rootOperationId, bool withdrawal)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.LogicalId == Uuid.Empty || operation.Owner != Stream || operation.RootOperationId != rootOperationId)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A guard completion requires its own logical operation under the root it settles."));
        return _pending.TryGetValue(rootOperationId, out var pending) && pending.IsWithdrawal == withdrawal &&
               _contributions.ContainsKey(pending.Episode)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The guard has no such unfinished operation to settle."));
    }

    bool OtherAvailable(Uuid episode) =>
        _contributions.Any(pair => pair.Key != episode && pair.Value.Available);

    Contribution ContributionOf(Uuid episode)
    {
        if (!_contributions.TryGetValue(episode, out var contribution))
            _contributions[episode] = contribution = new Contribution(false, 0, []);
        return contribution;
    }

    void Observe(DomainEvent fact, ImmutableContentRef? proofs)
    {
        Acceptance.Observe(fact);
        if (_hasFacts)
            return;
        _hasFacts = true;
        BaselineRef = proofs;
    }

    static Uuid EpisodeOf(OperationProvenance operation) =>
        Uuid.TryParse(operation.Episode, System.Globalization.CultureInfo.InvariantCulture, out var episode)
            ? episode
            : throw new InvalidOperationException("A guard fact must name its member episode.");

    sealed class Contribution
    {
        public Contribution(bool registered, long assignmentGeneration, IEnumerable<string> reasons)
        {
            IsRegistered = registered;
            AssignmentGeneration = assignmentGeneration;
            Reasons = new HashSet<string>(reasons, StringComparer.Ordinal);
            Available = registered && Reasons.Count == 0;
        }

        public bool IsRegistered { get; set; }

        public long AssignmentGeneration { get; set; }

        public HashSet<string> Reasons { get; }

        public bool Available { get; set; }
    }

    public static TenantManagerGuardRules FromCurrent(Uuid tenantId, ulong version,
        IEnumerable<TenantManagerEpisodeAuthority> authority, IReadOnlyDictionary<Uuid, PendingTransition> pending,
        bool hasFacts, ImmutableContentRef? baseline)
    {
        var model = new TenantManagerGuardRules(tenantId) { Version = version, _hasFacts = hasFacts, BaselineRef = baseline };
        foreach (var value in authority)
        {
            model._members[value.EpisodeId] = value.MemberId;
            if (value.HasContribution)
                model._contributions[value.EpisodeId] = new Contribution(value.IsRegistered, value.AssignmentGeneration, value.Reasons) { Available = value.Available };
            if (value.NeutralReasons.Count != 0)
                model._neutral[value.EpisodeId] = new HashSet<string>(value.NeutralReasons, StringComparer.Ordinal);
        }
        foreach (var pair in pending)
            model._pending.Add(pair.Key, pair.Value);
        return model;
    }

    public IReadOnlyList<TenantManagerEpisodeAuthority> Authority => _members.Keys.Select(Current).ToArray();
    public TenantManagerEpisodeAuthority Current(Uuid episode)
    {
        _contributions.TryGetValue(episode, out var contribution);
        return new TenantManagerEpisodeAuthority(episode, _members.GetValueOrDefault(episode), contribution is not null,
            contribution?.IsRegistered ?? false, contribution?.AssignmentGeneration ?? 0,
            contribution?.Reasons.Order(StringComparer.Ordinal).ToArray() ?? [], contribution?.Available ?? false,
            _neutral.GetValueOrDefault(episode)?.Order(StringComparer.Ordinal).ToArray() ?? []);
    }
    public void ObserveFact(DomainEvent fact)
    {
        if (fact is not ITenantManagerAuthorityFact native || !Uuid.TryParse(native.Operation.Episode,
                System.Globalization.CultureInfo.InvariantCulture, out var episode))
            throw new InvalidOperationException("Every guard fact names its exact membership episode.");
        if (native.CurrentAuthority is { } authority)
            RegisterMember(episode, authority.MemberId);
        if (!_handlers.TryGetValue(fact.GetType(), out var apply))
            throw new InvalidOperationException("The guard stream contains an unsupported native fact.");
        apply(fact);
        Version++;
    }
    void RegisterMember(Uuid episode, Uuid memberId)
    {
        if (memberId == Uuid.Empty || _members.TryGetValue(episode, out var existing) && existing != memberId)
            throw new InvalidOperationException("A membership episode belongs to one exact member identity.");
        _members[episode] = memberId;
    }
    void On<T>(Action<T> apply) where T : DomainEvent => _handlers.Add(typeof(T), value => apply((T)value));
    void RaiseEvent(DomainEvent fact)
    {
        ObserveFact(fact);
        var episode = EpisodeOf(((INativeOperationFact)fact).Operation);
        var authority = Current(episode);
        _emitted.Add(fact switch
        {
            TenantManagerWithdrawalAccepted value => value with { CurrentAuthority = authority },
            TenantManagerWithdrawalCompleted value => value with { CurrentAuthority = authority },
            TenantManagerRestorationAccepted value => value with { CurrentAuthority = authority },
            TenantManagerRestorationCompleted value => value with { CurrentAuthority = authority },
            TenantManagerSourceMutationAdmitted value => value with { CurrentAuthority = authority },
            TenantManagerRestored value => value with { CurrentAuthority = authority },
            _ => throw new InvalidOperationException("Unsupported guard producer."),
        });
    }

}
