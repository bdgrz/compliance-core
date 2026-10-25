using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     A policy acknowledgement or training campaign (M0-D12): exact material, a frozen launch audience from one
///     roster snapshot, attributed joiner/mover/leaver amendments, the admission of complete participant results on
///     its open generation, and a sealed closure that drains them. The logical revision counts the facts of this
///     owner that a caller must observe (launch, reconciliation, admitted result, closure acceptance, cancellation
///     and closure). The per-person results live on <see cref="PolicyCampaignParticipant" />.
/// </summary>
public sealed class PolicyDistributionCampaign : Aggregate
{
    /// <summary>Stream area of every campaign (the realm is the tenant).</summary>
    public const string Area = "policy-distribution-campaigns";

    /// <summary>The acknowledgement text a policy campaign shows and every acknowledgement must store.</summary>
    public const string DefaultAcknowledgementText =
        "I have read, understood, and agree to comply with this exact policy version.";

    /// <summary>Days after the start of work within which a joiner is due.</summary>
    public const int JoinerDays = 30;

    readonly Uuid _tenantId;
    readonly List<PolicyCampaignAudienceAmended> _amendments = [];
    readonly List<PolicyParticipantResultAdmitted> _admitted = [];
    readonly Dictionary<Uuid, PolicyParticipantResultAdmitted> _admittedByRoot = [];
    readonly Dictionary<Uuid, PolicyCampaignClosureAccepted> _closures = [];
    readonly Dictionary<Uuid, PolicyCampaignClosureCanceled> _canceled = [];
    readonly HashSet<Uuid> _reconciledSnapshots = [];
    PolicyCampaignLaunched? _launch;
    PolicyCampaignAudienceFrozen? _frozen;
    PolicyCampaignClosureAccepted? _pending;
    PolicyCampaignClosed? _closed;
    long _revision;
    long _reconciliations;

    /// <summary>Opens the campaign with a known id, selecting the logical operation to resolve.</summary>
    public PolicyDistributionCampaign(Uuid tenantId, Uuid campaignId, Uuid selectedLogicalId = default)
        : base(campaignId, new EventStreamAddress(tenantId.ToString(), Area, campaignId.ToString()))
    {
        _tenantId = tenantId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<PolicyCampaignLaunched>(ev =>
        {
            if (ev.Actor is not { Kind: "member" } || ev.ActorMemberId == Uuid.Empty ||
                !Uuid.TryParse(ev.Actor.Id, System.Globalization.CultureInfo.InvariantCulture, out var memberId) ||
                memberId != ev.ActorMemberId || ev.Operation is null || ev.Operation.Actor != ev.Actor)
                throw new ImmutableContentIntegrityException("The campaign launching member differs from its actual native actor.");
            Acceptance.Observe(ev);
            if (_launch is not null || ev.TenantId != _tenantId || ev.CampaignId != Id || ev.Operation.Owner != Stream ||
                ev.Operation.LogicalId != Id || ev.FirstResponse is null)
                throw new InvalidOperationException("A campaign is launched once, on the stream its tenant and id derive.");
            _launch = ev;
            _revision = 1;
            _reconciledSnapshots.Add(ev.RosterSnapshotId);
            LatestRosterSnapshotId = ev.RosterSnapshotId;
        });
        On<PolicyCampaignAudienceFrozen>(ev =>
        {
            Acceptance.Observe(ev);
            if (_launch is null || _frozen is not null || ev.TenantId != _tenantId || ev.CampaignId != Id ||
                ev.Operation.Owner != Stream)
                throw new InvalidOperationException("The audience is frozen once, right after the launch.");
            _frozen = ev;
        });
        On<PolicyCampaignAudienceAmended>(ev =>
        {
            Acceptance.Observe(ev);
            if (_frozen is null || IsSealed || ev.TenantId != _tenantId || ev.CampaignId != Id ||
                ev.Operation.Owner != Stream || ev.Reconciliation != _reconciliations + 1 || ev.Batch != 1 ||
                !_reconciledSnapshots.Add(ev.RosterSnapshotId))
                throw new InvalidOperationException("A reconciliation follows the previous one on an open campaign.");
            _reconciliations = ev.Reconciliation;
            LatestRosterSnapshotId = ev.RosterSnapshotId;
            _revision++;
            _amendments.Add(ev);
        });
        On<PolicyParticipantResultAdmitted>(ev =>
        {
            Acceptance.Observe(ev);
            if (_frozen is null || IsSealed || ev.Owner != Stream || ev.Operation.Owner != Stream ||
                ev.Revision != _revision + 1 || ev.Generation != Generation ||
                !_admittedByRoot.TryAdd(ev.Operation.RootOperationId, ev) || ev.FirstResponse is null)
                throw new InvalidOperationException("A result is admitted on the open generation at the next revision.");
            _revision = ev.Revision;
            _admitted.Add(ev);
        });
        On<PolicyCampaignClosureAccepted>(ev =>
        {
            Acceptance.Observe(ev);
            if (_frozen is null || IsSealed || ev.Owner != Stream || ev.Operation.Owner != Stream ||
                ev.Revision != _revision + 1 || ev.Generation != Generation || ev.FirstResponse is null ||
                !_closures.TryAdd(ev.Operation.RootOperationId, ev))
                throw new InvalidOperationException("A closure is accepted once on the open generation at the next revision.");
            _revision = ev.Revision;
            _pending = ev;
        });
        On<PolicyCampaignClosureCanceled>(ev =>
        {
            Acceptance.Observe(ev);
            if (_pending is null || ev.Owner != Stream || ev.Operation.Owner != Stream ||
                ev.Operation.RootOperationId != _pending.Operation.RootOperationId || ev.Intent != _pending.Intent ||
                ev.Revision != _revision + 1 || ev.Generation != _pending.Generation)
                throw new InvalidOperationException("Only the exact pending closure can be canceled.");
            _revision = ev.Revision;
            _canceled.Add(ev.Operation.RootOperationId, ev);
            _pending = null;
        });
        On<PolicyCampaignClosed>(ev =>
        {
            Acceptance.Observe(ev);
            if (_pending is null || ev.TenantId != _tenantId || ev.CampaignId != Id || ev.Operation.Owner != Stream ||
                ev.Operation.RootOperationId != _pending.Operation.RootOperationId || ev.Intent != _pending.Intent ||
                ev.AudienceGeneration != _reconciliations)
                throw new InvalidOperationException("Only the exact pending closure can complete.");
            _revision++;
            _closed = ev;
            _pending = null;
        });
    }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The roster snapshot the audience was last reconciled against.</summary>
    public Uuid LatestRosterSnapshotId { get; private set; }

    /// <summary>True once the campaign was launched.</summary>
    public bool IsLaunched => _launch is not null && _frozen is not null;

    /// <summary>True once the closure completed.</summary>
    public bool IsClosed => _closed is not null;

    /// <summary>True while result admission is sealed: a closure is pending or completed.</summary>
    public bool IsSealed => _pending is not null || _closed is not null;

    /// <summary>The tenant of the campaign.</summary>
    public Uuid TenantId => _tenantId;

    /// <summary>The campaign's logical revision.</summary>
    public long Revision => _revision;

    /// <summary>The open admission generation: one more than the number of canceled closures.</summary>
    public long Generation => _canceled.Count + 1;

    /// <summary>The number of reconciliations, which is the audience generation.</summary>
    public long Reconciliations => _reconciliations;

    /// <summary>The program of the campaign.</summary>
    public Uuid ProgramId => _launch?.ProgramId ?? Uuid.Empty;

    /// <summary>The launching member, who owns the campaign's work.</summary>
    public Uuid OwnerMemberId => _launch?.ActorMemberId ?? Uuid.Empty;

    /// <summary>The launch instant.</summary>
    public DateTimeOffset LaunchedAt => _launch?.LaunchedAt ?? DateTimeOffset.MinValue;

    /// <summary>The material the campaign distributes.</summary>
    public CampaignSubject? Subject => _launch?.Subject;

    /// <summary>The launch fact, once recorded.</summary>
    public PolicyCampaignLaunched? Launch => _launch;

    /// <summary>The frozen audience fact, once recorded.</summary>
    public PolicyCampaignAudienceFrozen? Frozen => _frozen;

    /// <summary>Every reconciliation in order.</summary>
    public IReadOnlyList<PolicyCampaignAudienceAmended> Amendments => _amendments;

    /// <summary>Every admitted result in order.</summary>
    public IReadOnlyList<PolicyParticipantResultAdmitted> Admitted => _admitted;

    /// <summary>The accepted closure that is neither completed nor canceled.</summary>
    public PolicyCampaignClosureAccepted? PendingClosure => _pending;

    /// <summary>The terminal closure fact, once recorded.</summary>
    public PolicyCampaignClosed? Closing => _closed;

    /// <summary>True when a roster snapshot was already reconciled (or launched).</summary>
    public bool HasReconciled(Uuid snapshotId) => _reconciledSnapshots.Contains(snapshotId);

    /// <summary>The admission of a root operation, once recorded.</summary>
    public PolicyParticipantResultAdmitted? AdmittedFor(Uuid rootOperationId) =>
        _admittedByRoot.GetValueOrDefault(rootOperationId);

    /// <summary>The accepted closure of a root operation, once recorded.</summary>
    public PolicyCampaignClosureAccepted? ClosureFor(Uuid rootOperationId) =>
        _closures.GetValueOrDefault(rootOperationId);

    /// <summary>The cancellation of a root operation's closure, once recorded.</summary>
    public PolicyCampaignClosureCanceled? CanceledFor(Uuid rootOperationId) =>
        _canceled.GetValueOrDefault(rootOperationId);

    /// <summary>The results admitted for one participant stream, in admission order.</summary>
    public IReadOnlyList<PolicyParticipantResultAdmitted> AdmittedFor(EventStreamAddress participant) =>
        _admitted.Where(admitted => admitted.Operation.AffectedOwner == participant).ToArray();

    /// <summary>The result kind an admission carries, derived from its command kind.</summary>
    public static CampaignParticipantResultKind KindOf(PolicyParticipantResultAdmitted admitted)
    {
        ArgumentNullException.ThrowIfNull(admitted);
        return admitted.Operation.CommandKind switch
        {
            nameof(AcknowledgePolicy) => CampaignParticipantResultKind.PolicyAcknowledgement,
            nameof(RecordTrainingCompletion) => CampaignParticipantResultKind.TrainingCompletion,
            nameof(ApproveCampaignWaiver) => CampaignParticipantResultKind.Waiver,
            _ => throw new InvalidOperationException($"{admitted.Operation.CommandKind} is not a participant result."),
        };
    }

    /// <summary>Checks the launch guards without raising anything.</summary>
    public Result PrepareLaunch() => _launch is not null
        ? Conflict("The campaign was already launched with different terms.")
        : Result.Success;

    /// <summary>Launches the campaign and freezes its audience as the root native facts of the launch.</summary>
    public Result LaunchCampaign(OperationProvenance operation, Uuid programId, CampaignSubject subject,
        string audienceKind, ImmutableContentRef audienceTeams, Uuid rosterSnapshotId, string rosterContentSha256,
        DateOnly launchedOn, DateOnly dueOn, string? instructions, int audienceCount, ActorReference actor,
        Uuid actorMemberId, DateTimeOffset launchedAt, ImmutableContentRef entries, ImmutableContentRef firstResponse,
        ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(subject);
        if (operation.Owner != Stream || operation.LogicalId != Id)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A launch is the root operation of its own campaign."));
        var prepared = PrepareLaunch();
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new PolicyCampaignLaunched(_tenantId, programId, Id, subject, audienceKind, audienceTeams,
            rosterSnapshotId, rosterContentSha256, launchedOn, dueOn, instructions,
            subject.Kind == "policy" ? DefaultAcknowledgementText : null, audienceCount, actor, actorMemberId,
            launchedAt, operation, firstResponse, operationResult));
        // The frozen audience is part of the launch: its own logical id keeps one root fact per logical id.
        RaiseEvent(new PolicyCampaignAudienceFrozen(_tenantId, Id, 1, entries,
            operation with { LogicalId = Kernel.AggregateIdentity.Create("PolicyCampaignAudienceFrozen", Id) },
            null, null));
        return Result.Success;
    }

    /// <summary>Checks the guards every admission of a caller operation shares, without raising anything.</summary>
    public Result PrepareOpenAdmission(long expectedRevision)
    {
        if (!IsLaunched)
            return NotFound();
        if (IsClosed)
            return Conflict("The campaign is closed.");
        if (IsSealed)
            return Conflict("The campaign is closing; its result admission is sealed.");
        return expectedRevision == _revision ? Result.Success : Stale();
    }

    /// <summary>Records the reconciliation as the root native fact of its operation.</summary>
    public Result Reconcile(OperationProvenance operation, Uuid rosterSnapshotId, string rosterContentSha256,
        DateOnly observedOn, ImmutableContentRef amendments, ActorReference actor, DateTimeOffset amendedAt,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A reconciliation is a root operation of its own campaign with an expected revision."));
        var prepared = PrepareOpenAdmission((long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        if (_reconciledSnapshots.Contains(rosterSnapshotId))
            return Conflict("The campaign already reconciled that roster snapshot.");
        RaiseEvent(new PolicyCampaignAudienceAmended(_tenantId, Id, _reconciliations + 1, 1, rosterSnapshotId,
            rosterContentSha256, observedOn, amendments, actor, amendedAt, operation, firstResponse,
            operationResult));
        return Result.Success;
    }

    /// <summary>Admits one complete participant result on the open generation as the root native fact of its operation.</summary>
    public Result AdmitResult(OperationProvenance operation, ImmutableContentRef intent,
        ImmutableContentRef firstResponse)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A participant result is a root operation of its own campaign with an expected revision."));
        var prepared = PrepareOpenAdmission((long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new PolicyParticipantResultAdmitted(Stream, _revision + 1, Generation, intent, null, operation,
            firstResponse, null));
        return Result.Success;
    }

    /// <summary>Accepts the closure as the root native fact of its operation and seals result admission.</summary>
    public Result AcceptClosure(OperationProvenance operation, ImmutableContentRef intent,
        ImmutableContentRef firstResponse)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A closure is a root operation of its own campaign with an expected revision."));
        var prepared = PrepareOpenAdmission((long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new PolicyCampaignClosureAccepted(Stream, _revision + 1, Generation, intent, null, operation,
            firstResponse, null));
        return Result.Success;
    }

    /// <summary>Cancels the pending closure and reopens result admission on the next generation.</summary>
    public Result CancelClosure(OperationProvenance operation, ImmutableContentRef failureProof)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (_pending is null || _pending.Operation.RootOperationId != operation.RootOperationId)
            return Conflict("Only the exact pending closure can be canceled.");
        RaiseEvent(new PolicyCampaignClosureCanceled(Stream, _revision + 1, _pending.Generation, failureProof,
            null, operation, null, null));
        return Result.Success;
    }

    /// <summary>Completes the pending closure with the frozen totals and the complete participant proofs.</summary>
    public Result CompleteClosure(OperationProvenance operation, DateOnly closedOn, string rationale,
        ImmutableContentRef totals, ActorReference actor, DateTimeOffset closedAt, ImmutableContentRef participantProofs,
        ImmutableContentRef intent, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (_pending is null || _pending.Operation.RootOperationId != operation.RootOperationId ||
            _pending.Intent != intent)
            return Conflict("Only the exact pending closure can complete.");
        RaiseEvent(new PolicyCampaignClosed(_tenantId, Id, closedOn, rationale, totals, actor, closedAt, operation,
            _reconciliations, participantProofs, intent, null, operationResult));
        return Result.Success;
    }

    Result Stale() => Result.Failure(VersionedRecordRules.StaleRevision("campaign", _revision).ToRequestError());

    static Result Conflict(string message) => Result.Failure(new RequestError(RequestErrorKind.Conflict, message));

    static Result NotFound() => Result.Failure(new RequestError(RequestErrorKind.NotFound, "The campaign was not found."));
}
