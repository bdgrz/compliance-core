using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Events;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     The treatment assertion line of one risk and one control: at most one pending proposal and at most one accepted
///     version at a time. Accepting a proposal supersedes the previously accepted version of the same control; a
///     retirement only retires the accepted treatment it names and never a newer successor. The logical revision counts
///     the facts of this line only, so lines of different risks or controls are independent owners.
/// </summary>
public sealed class RiskTreatmentAssertionRules
{
    /// <summary>Stream area of every control treatment assertion line (the realm is the tenant).</summary>
    public const string Area = "risk-control-treatments";

    /// <summary>The identity owner type of a control treatment assertion line.</summary>
    public const string OwnerType = "RiskTreatmentAssertion";

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    readonly Uuid _riskId;
    readonly Uuid _controlId;
    readonly List<RiskTreatmentLine> _lines = [];
    readonly List<DomainEvent> _facts = [];
    long _revision;

    /// <summary>Opens one line, selecting the logical operation to resolve.</summary>
    public RiskTreatmentAssertionRules(Uuid tenantId, Uuid programId, Uuid riskId, Uuid controlId)
    {
        (_tenantId, _programId, _riskId, _controlId) = (tenantId, programId, riskId, controlId);
    }

    /// <summary>The identity of the line of a risk and control.</summary>
    public static Uuid IdentityOf(Uuid tenantId, Uuid programId, Uuid riskId, Uuid controlId) =>
        AggregateIdentity.Create(OwnerType, tenantId, programId, riskId, controlId);

    public Uuid Id => IdentityOf(_tenantId, _programId, _riskId, _controlId);
    public EventStreamAddress Stream => new(_tenantId.ToString(), Area, Id.ToString());
    public static RiskTreatmentAssertionRules FromFacts(Uuid tenant, Uuid program, Uuid risk, Uuid control, IReadOnlyList<DomainEvent> facts)
    {
        var rules = new RiskTreatmentAssertionRules(tenant, program, risk, control);
        foreach (var fact in facts)
            rules.RecordFact(fact);
        return rules;
    }
    void RecordFact(DomainEvent fact)
    {
        switch (fact)
        {
            case RiskControlTreatmentProposed proposed:
                Apply(proposed);
                break;
            case RiskControlTreatmentReviewed reviewed:
                Apply(reviewed);
                break;
            case RiskControlTreatmentRetired retired:
                Apply(retired);
                break;
            default:
                throw new InvalidOperationException("An assertion source contains another fact type.");
        }
    }

    /// <summary>The risk this line belongs to.</summary>
    public Uuid RiskId => _riskId;

    /// <summary>The control this line asserts treatment by.</summary>
    public Uuid ControlId => _controlId;

    /// <summary>The logical revision: the number of facts of this line.</summary>
    public long Revision => _revision;

    /// <summary>Every treatment of the line in the order proposed.</summary>
    public IReadOnlyList<RiskTreatmentLine> Treatments => _lines;

    /// <summary>Every fact in order.</summary>
    public IReadOnlyList<DomainEvent> Facts => _facts;

    /// <summary>The treatment awaiting review, or null.</summary>
    public RiskTreatmentLine? Pending => _lines.FirstOrDefault(static line => line.Status == RiskStates.TreatmentProposed);

    /// <summary>The accepted treatment in force, or null.</summary>
    public RiskTreatmentLine? Accepted => _lines.FirstOrDefault(static line => line.Status == RiskStates.TreatmentAccepted);

    /// <summary>The treatment with this identity, or null.</summary>
    public RiskTreatmentLine? Find(Uuid treatmentId) => _lines.FirstOrDefault(line => line.TreatmentId == treatmentId);

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
        if (Find(treatmentId) is not { } line)
            return Fail(CommandFailure.MissingRecord("The risk control treatment was not found."));
        if (line.Status != RiskStates.TreatmentProposed)
            return Fail(CommandFailure.StateConflict("The control treatment has no pending assertion to review."));
        return expectedRevision != _revision ? Stale() : Result.Success;
    }

    /// <summary>Checks that the accepted treatment can be retired at the expected revision.</summary>
    public Result PrepareRetire(Uuid treatmentId, long expectedRevision)
    {
        if (Find(treatmentId) is not { } line)
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
        RecordFact(new RiskControlTreatmentProposed(_tenantId, _programId, _riskId, _revision + 1, treatmentContent,
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
        RecordFact(new RiskControlTreatmentReviewed(_tenantId, _programId, _riskId, _revision + 1, treatmentId,
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
        RecordFact(new RiskControlTreatmentRetired(_tenantId, _programId, _riskId, _revision + 1, treatmentId,
            rationale, retiredBy, retiredAt, operation, firstResponse, operationResult));
        return Result.Success;
    }

    void Apply(RiskControlTreatmentProposed ev)
    {
        EnsureNext(ev.TenantId, ev.ProgramId, ev.RiskId, ev.Revision, ev.Operation);
        if (Pending is not null || Find(ev.Operation.LogicalId) is not null)
            throw new InvalidOperationException("A line holds one pending proposal and never reuses a treatment id.");
        _lines.Add(new RiskTreatmentLine(ev));
        Advance(ev);
    }

    void Apply(RiskControlTreatmentReviewed ev)
    {
        EnsureNext(ev.TenantId, ev.ProgramId, ev.RiskId, ev.Revision, ev.Operation);
        var line = Find(ev.TreatmentId);
        if (line is null || line.Status != RiskStates.TreatmentProposed)
            throw new InvalidOperationException("Only the pending proposal of the line is reviewed.");
        if (ev.Outcome == RiskStates.Accept)
            foreach (var previous in _lines.Where(static item => item.Status == RiskStates.TreatmentAccepted))
                previous.Status = RiskStates.TreatmentSuperseded;
        line.Reviewed = ev;
        line.Status = ev.Outcome == RiskStates.Accept ? RiskStates.TreatmentAccepted : RiskStates.TreatmentRejected;
        Advance(ev);
    }

    void Apply(RiskControlTreatmentRetired ev)
    {
        EnsureNext(ev.TenantId, ev.ProgramId, ev.RiskId, ev.Revision, ev.Operation);
        var line = Find(ev.TreatmentId);
        if (line is null || line.Status != RiskStates.TreatmentAccepted)
            throw new InvalidOperationException("Only the accepted treatment of the line is retired.");
        line.Retired = ev;
        line.Status = RiskStates.TreatmentRetired;
        Advance(ev);
    }

    void EnsureNext(Uuid tenantId, Uuid programId, Uuid riskId, long revision, OperationProvenance operation)
    {
        if (tenantId != _tenantId || programId != _programId || riskId != _riskId || revision != _revision + 1 ||
            operation.Owner != Stream)
            throw new InvalidOperationException("A line fact follows the previous one at the next revision.");
    }

    void Advance(DomainEvent fact)
    {
        _revision++;
        _facts.Add(fact);
    }

    Result PrepareOwn(OperationProvenance operation, Func<long, Result> prepare)
    {
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A line write is a root operation of its own line with an expected revision."));
        return prepare((long)expected);
    }

    Result Stale() => Fail(CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("risk control treatment",
        _revision)));

    static Result Fail(CommandFailure failure) => Result.Failure(failure.ToRequestError());
}
