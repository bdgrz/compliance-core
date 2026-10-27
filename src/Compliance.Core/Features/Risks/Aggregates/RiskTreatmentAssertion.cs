using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Versioning.Rules;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Events;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
namespace Bdgrz.Compliance.Features.Risks.Aggregates;

/// <summary>
///     The treatment assertion line of one risk and one control: at most one pending proposal and at most one accepted
///     version at a time. Accepting a proposal supersedes the previously accepted version of the same control; a
///     retirement only retires the accepted treatment it names and never a newer successor. The logical revision counts
///     the facts of this line only, so lines of different risks or controls are independent owners.
/// </summary>
public sealed class RiskTreatmentAssertion : Aggregate
{
    /// <summary>Stream area of every control treatment assertion line (the realm is the tenant).</summary>
    public const string Area = "risk-control-treatments";

    /// <summary>The identity owner type of a control treatment assertion line.</summary>
    public const string OwnerType = "RiskTreatmentAssertion";

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    readonly Uuid _riskId;
    readonly Uuid _controlId;
    RiskTreatmentLine? _pending;
    RiskTreatmentLine? _accepted;
    DomainEvent? _latest;
    readonly bool _replayOnly;
    long _revision;

    /// <summary>Opens one line, selecting the logical operation to resolve.</summary>
    public RiskTreatmentAssertion(Uuid tenantId, Uuid programId, Uuid riskId, Uuid controlId,
        Uuid selectedLogicalId = default)
        : base(IdentityOf(tenantId, programId, riskId, controlId), new EventStreamAddress(tenantId.ToString(), Area,
            IdentityOf(tenantId, programId, riskId, controlId).ToString()))
    {
        (_tenantId, _programId, _riskId, _controlId) = (tenantId, programId, riskId, controlId);
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<RiskControlTreatmentProposed>(Apply);
        On<RiskControlTreatmentReviewed>(Apply);
        On<RiskControlTreatmentRetired>(Apply);
    }

    RiskTreatmentAssertion(Uuid tenant, Uuid program, Uuid risk, EventStreamAddress source, Uuid logical)
        : base(Uuid.Parse(source.Resource, System.Globalization.CultureInfo.InvariantCulture), source)
    {
        (_tenantId, _programId, _riskId, _controlId) = (tenant, program, risk, Uuid.Empty);
        _replayOnly = true;
        Acceptance = new NativeAcceptance(logical);
        On<RiskControlTreatmentProposed>(Apply);
        On<RiskControlTreatmentReviewed>(Apply);
        On<RiskControlTreatmentRetired>(Apply);
    }

    internal static RiskTreatmentAssertion ForAcceptedReplay(Uuid tenant, Uuid program, Uuid risk,
        EventStreamAddress source, Uuid logical) => source.Realm == tenant.ToString() && source.Area == Area
            ? new RiskTreatmentAssertion(tenant, program, risk, source, logical)
            : throw new InvalidOperationException("An accepted assertion owner must belong to the exact requested tenant scope.");

    /// <summary>The identity of the line of a risk and control.</summary>
    public static Uuid IdentityOf(Uuid tenantId, Uuid programId, Uuid riskId, Uuid controlId) =>
        AggregateIdentity.Create(OwnerType, tenantId, programId, riskId, controlId);

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The risk this line belongs to.</summary>
    public Uuid RiskId => _riskId;

    /// <summary>The control this line asserts treatment by.</summary>
    public Uuid ControlId => _controlId;

    /// <summary>The logical revision: the number of facts of this line.</summary>
    public long Revision => _revision;

    public DomainEvent? Latest => _latest;
    public Uuid TenantId => _tenantId;
    public Uuid ProgramId => _programId;
    public RiskTreatmentLine? Pending => _pending;
    public RiskTreatmentLine? Accepted => _accepted;

    /// <summary>Checks that a treatment can be proposed at the expected revision.</summary>
    public Result PreparePropose(long expectedRevision)
    {
        if (expectedRevision != _revision)
            return Stale();
        return Pending is not null
            ? Fail(CommandFailure.StateConflict("The control already has a pending treatment assertion for this risk."))
            : Result.Success;
    }

    /// <summary>Checks that the pending treatment can be reviewed at the expected revision.</summary>
    public Result PrepareReview(Uuid treatmentId, long expectedRevision)
    {
        if (Pending is not { } line || line.TreatmentId != treatmentId)
            return Fail(CommandFailure.MissingRecord("The risk control treatment was not found."));
        if (line.Status != RiskStates.TreatmentProposed)
            return Fail(CommandFailure.StateConflict("The control treatment has no pending assertion to review."));
        return expectedRevision != _revision ? Stale() : Result.Success;
    }

    /// <summary>Checks that the accepted treatment can be retired at the expected revision.</summary>
    public Result PrepareRetire(Uuid treatmentId, long expectedRevision)
    {
        if (Accepted is not { } line || line.TreatmentId != treatmentId)
            return Fail(CommandFailure.MissingRecord("The risk control treatment was not found."));
        if (expectedRevision != _revision)
            return Stale();
        return line.Status != RiskStates.TreatmentAccepted
            ? Fail(CommandFailure.StateConflict("Only an accepted control treatment assertion can be retired."))
            : Result.Success;
    }

    /// <summary>Proposes a treatment as the root native fact of its operation; the logical id is the treatment id.</summary>
    public Result Propose(OperationProvenance operation, ImmutableContentRef treatmentContent, Uuid proposerMemberId,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(treatmentContent);
        var prepared = PrepareOwn(operation, PreparePropose);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new RiskControlTreatmentProposed(_tenantId, _programId, _riskId, _revision + 1, treatmentContent,
            proposerMemberId, operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Records the independent review of a treatment as the root native fact of its operation.</summary>
    public Result Review(OperationProvenance operation, Uuid treatmentId, string outcome, string rationale,
        ActorReference reviewer, DateTimeOffset reviewedAt, Uuid? waiverId, ImmutableContentRef firstResponse,
        ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(reviewer);
        var prepared = PrepareOwn(operation, expected => PrepareReview(treatmentId, expected));
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new RiskControlTreatmentReviewed(_tenantId, _programId, _riskId, _revision + 1, treatmentId,
            operation.LogicalId, outcome, rationale, reviewer, reviewedAt, waiverId, operation, firstResponse,
            operationResult));
        return Result.Success;
    }

    /// <summary>Retires the accepted treatment as the root native fact of its operation.</summary>
    public Result Retire(OperationProvenance operation, Uuid treatmentId, string rationale, ActorReference retiredBy,
        DateTimeOffset retiredAt, ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(retiredBy);
        var prepared = PrepareOwn(operation, expected => PrepareRetire(treatmentId, expected));
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new RiskControlTreatmentRetired(_tenantId, _programId, _riskId, _revision + 1, treatmentId,
            rationale, retiredBy, retiredAt, operation, firstResponse, operationResult));
        return Result.Success;
    }

    void Apply(RiskControlTreatmentProposed ev)
    {
        if (ev.Operation.CommandKind != nameof(ProposeRiskControlTreatment) || ev.Operation.Actor.Id != ev.ProposerMemberId.ToString() ||
            ev.TreatmentContent.Schema != RiskContent.TreatmentSchema || ev.FirstResponse is null || ev.OperationResult is null)
            throw new InvalidOperationException("The assertion proposal has another exact native kind or proposer.");
        EnsureNext(ev.TenantId, ev.ProgramId, ev.RiskId, ev.Revision, ev.Operation);
        Acceptance.Observe(ev);
        if (Pending is not null || Accepted?.TreatmentId == ev.Operation.LogicalId)
            throw new InvalidOperationException("A line holds one pending proposal and never reuses a treatment id.");
        _pending = new RiskTreatmentLine(ev);
        Advance(ev);
    }

    void Apply(RiskControlTreatmentReviewed ev)
    {
        if (ev.Operation.CommandKind != nameof(ReviewRiskControlTreatment) || ev.Reviewer != ev.Operation.Actor || ev.ReviewedAt != ev.Operation.AcceptedAt ||
            ev.DecisionId != ev.Operation.LogicalId || ev.FirstResponse is null || ev.OperationResult is null)
            throw new InvalidOperationException("The assertion review has another exact native decision or actor.");
        EnsureNext(ev.TenantId, ev.ProgramId, ev.RiskId, ev.Revision, ev.Operation);
        Acceptance.Observe(ev);
        var line = Pending?.TreatmentId == ev.TreatmentId ? Pending : null;
        if (line is null || line.Status != RiskStates.TreatmentProposed)
            throw new InvalidOperationException("Only the pending proposal of the line is reviewed.");
        if (ev.Outcome is not (RiskStates.Accept or RiskStates.Reject))
            throw new InvalidOperationException("An assertion review requires its exact accepted or rejected outcome.");
        line.Reviewed = ev;
        line.Status = ev.Outcome == RiskStates.Accept ? RiskStates.TreatmentAccepted : RiskStates.TreatmentRejected;
        if (ev.Outcome == RiskStates.Accept)
            _accepted = line;
        _pending = null;
        Advance(ev);
    }

    void Apply(RiskControlTreatmentRetired ev)
    {
        if (ev.Operation.CommandKind != nameof(RetireRiskControlTreatment) || ev.RetiredBy != ev.Operation.Actor || ev.RetiredAt != ev.Operation.AcceptedAt ||
            ev.FirstResponse is null || ev.OperationResult is null)
            throw new InvalidOperationException("The assertion retirement has another exact native kind or actor.");
        EnsureNext(ev.TenantId, ev.ProgramId, ev.RiskId, ev.Revision, ev.Operation);
        Acceptance.Observe(ev);
        var line = Accepted?.TreatmentId == ev.TreatmentId ? Accepted : null;
        if (line is null || line.Status != RiskStates.TreatmentAccepted)
            throw new InvalidOperationException("Only the accepted treatment of the line is retired.");
        line.Retired = ev;
        line.Status = RiskStates.TreatmentRetired;
        _accepted = null;
        Advance(ev);
    }

    void EnsureNext(Uuid tenantId, Uuid programId, Uuid riskId, long revision, OperationProvenance operation)
    {
        if (tenantId != _tenantId || programId != _programId || riskId != _riskId || revision != _revision + 1 ||
            operation.Owner != Stream || (operation.FirstOwner ?? operation.Owner) != Stream || operation.Actor.Kind != "member" ||
            !Uuid.TryParse(operation.Actor.Id, out var actor) || actor == Uuid.Empty ||
            !NativeOwnerHistory.IsOwnRoot(this, operation, operation.CommandKind, (ulong)_revision) ||
            operation.CommandKind is not (nameof(ProposeRiskControlTreatment) or nameof(ReviewRiskControlTreatment) or nameof(RetireRiskControlTreatment)))
            throw new InvalidOperationException("A line fact follows the previous one at the next revision.");
    }

    void Advance(DomainEvent fact)
    {
        _revision++;
        _latest = fact;
    }

    Result PrepareOwn(OperationProvenance operation, Func<long, Result> prepare)
    {
        if (_replayOnly || operation.Owner != Stream || operation.LogicalId != Acceptance.SelectedLogicalId ||
            operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A line write is a root operation of its own line with an expected revision."));
        return prepare((long)expected);
    }

    Result Stale() => Fail(CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("risk control treatment",
        _revision)));

    static Result Fail(CommandFailure failure) => Result.Failure(failure.ToRequestError());
}
