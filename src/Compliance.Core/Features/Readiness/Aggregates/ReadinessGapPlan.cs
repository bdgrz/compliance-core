using Bdgrz.Compliance.Features.Readiness;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Readiness.Events;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Aggregates;

/// <summary>
///     The stable plan and advisor annotations of one gap, identified by its program and gap. The logical revision
///     counts plans only: an annotation never advances it, and an active readiness decision use does not advance it
///     either but blocks a new plan until the use settles. A decision holds the exact plan (or the absence of one)
///     through <see cref="SourceUseReservations" />, so a plan that changes before the hold is acquired fences the
///     decision. Reducers never read content or another owner.
/// </summary>
public sealed class ReadinessGapPlan : Aggregate, ISourceUseOwner
{
    /// <summary>Stream area of every gap plan (the realm is the tenant).</summary>
    public const string Area = "readiness-gap-plans";

    /// <summary>The identity owner type of a gap plan.</summary>
    public const string OwnerType = "ReadinessGapPlan";

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    readonly Uuid _gapId;
    long _revision;

    /// <summary>Opens the plan of one gap, selecting the logical operation to resolve.</summary>
    public ReadinessGapPlan(Uuid tenantId, Uuid programId, Uuid gapId, Uuid selectedLogicalId = default)
        : base(IdentityOf(tenantId, programId, gapId),
            new EventStreamAddress(tenantId.ToString(), Area, IdentityOf(tenantId, programId, gapId).ToString()))
    {
        (_tenantId, _programId, _gapId) = (tenantId, programId, gapId);
        Acceptance = new NativeAcceptance(selectedLogicalId);
        Uses = new SourceUseReservations(Stream);
        On<ReadinessGapPlanned>(Apply);
        On<ReadinessGapAnnotated>(Apply);
        On<ReadinessGapPlanReadinessDecisionUseAuthorized>(granted =>
        {
            Acceptance.Observe(granted);
            if (granted.Revision != Revision)
                throw new InvalidOperationException("A decision use is granted at the held plan revision.");
            Uses.ApplyGranted(SourceUsePurpose.ReadinessDecision, granted);
            LatestFact = granted;
        });
        On<ReadinessGapPlanReadinessDecisionUseSettled>(settled =>
        {
            Acceptance.Observe(settled);
            Uses.ApplySettled(SourceUsePurpose.ReadinessDecision, settled);
            LatestFact = settled;
        });
    }

    /// <summary>The identity of the plan of a gap of a program.</summary>
    public static Uuid IdentityOf(Uuid tenantId, Uuid programId, Uuid gapId) =>
        AggregateIdentity.Create(OwnerType, tenantId, programId, gapId);

    /// <inheritdoc />
    public SourceUseReservations Uses { get; }

    /// <inheritdoc />
    public NativeAcceptance Acceptance { get; }

    /// <summary>The gap identity.</summary>
    public Uuid GapId => _gapId;

    /// <summary>The program the gap belongs to.</summary>
    public Uuid ProgramId => _programId;

    /// <summary>The plan revision: the number of plans recorded; zero while the gap is unplanned.</summary>
    public long Revision => _revision;

    /// <summary>The latest plan fact, or null while the gap is unplanned.</summary>
    public ReadinessGapPlanned? Latest { get; private set; }

    /// <summary>The tenant of this command authority.</summary>
    public Uuid TenantId => _tenantId;

    /// <summary>The last native fact, including use grants and settlements, at the captured physical cut.</summary>
    public DomainEvent? LatestFact { get; private set; }

    /// <summary>Checks that a plan can be recorded at the expected plan revision.</summary>
    public Result PreparePlan(long expectedRevision)
    {
        if (Uses.HasActiveUse)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "A readiness decision holds this gap plan; retry when it settles.", true));
        return expectedRevision != Revision
            ? Fail(CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("readiness gap plan", Revision)))
            : Result.Success;
    }

    /// <summary>Checks that an annotation can be recorded at the expected plan revision.</summary>
    public Result PrepareAnnotate(long expectedRevision) => expectedRevision != Revision
        ? Fail(CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("readiness gap plan", Revision)))
        : Result.Success;

    /// <summary>Records a plan as the root native fact of its operation.</summary>
    public Result Plan(OperationProvenance operation, ImmutableContentRef planContent, ImmutableContentRef firstResponse,
        ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A plan is a root operation of its own gap with an expected revision."));
        var prepared = PreparePlan((long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new ReadinessGapPlanned(_tenantId, _programId, Revision + 1, planContent, operation, firstResponse,
            operationResult));
        return Result.Success;
    }

    /// <summary>Records an annotation as the root native fact of its operation; the plan revision does not advance.</summary>
    public Result Annotate(OperationProvenance operation, ImmutableContentRef annotationContent,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An annotation is a root operation of its own gap with an expected revision."));
        var prepared = PrepareAnnotate((long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new ReadinessGapAnnotated(_tenantId, _programId, Revision, annotationContent, operation,
            firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Holds this exact plan for a readiness decision; the plan revision does not advance.</summary>
    public Result AuthorizeReadinessDecisionUse(OperationProvenance operation, ImmutableContentRef intent, long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A decision use is recorded on the gap plan's own stream."));
        Uses.RequireForAppend(SourceUsePurpose.ReadinessDecision, operation, generation, Version, CommittedStreamPosition);
        RaiseEvent(new ReadinessGapPlanReadinessDecisionUseAuthorized(Stream, Revision, generation, intent, null,
            operation, null, null));
        return Result.Success;
    }

    /// <summary>Settles a decision use, or fences its key when no grant exists yet.</summary>
    public Result SettleReadinessDecisionUse(OperationProvenance operation, ImmutableContentRef intent, long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A settlement is recorded on the gap plan's own stream."));
        Uses.RequireForAppend(SourceUsePurpose.ReadinessDecision, operation, generation, Version, CommittedStreamPosition);
        RaiseEvent(new ReadinessGapPlanReadinessDecisionUseSettled(Stream, Revision, generation, intent, null,
            operation, null, null));
        return Result.Success;
    }

    void Apply(ReadinessGapPlanned ev)
    {
        Acceptance.Observe(ev);
        if (ev.TenantId != _tenantId || ev.ProgramId != _programId || ev.Revision != Revision + 1 ||
            ev.Operation.Owner != Stream)
            throw new InvalidOperationException("A plan follows the previous plan at the next revision.");
        _revision = ev.Revision;
        Latest = ev;
        LatestFact = ev;
    }

    void Apply(ReadinessGapAnnotated ev)
    {
        Acceptance.Observe(ev);
        if (ev.TenantId != _tenantId || ev.ProgramId != _programId || ev.Revision != Revision ||
            ev.Operation.Owner != Stream)
            throw new InvalidOperationException("An annotation is recorded at the held plan revision.");
        LatestFact = ev;
    }

    static Result Fail(CommandFailure failure) => Result.Failure(failure.ToRequestError());
    protected override void Apply(DomainEvent ev)
    {
        base.Apply(ev);
        Uses.ObserveOwnerFact(ev);
    }
}
