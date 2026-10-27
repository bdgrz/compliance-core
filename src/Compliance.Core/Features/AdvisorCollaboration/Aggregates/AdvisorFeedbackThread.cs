using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Aggregates;

/// <summary>
///     One feedback thread, identified by its tenant, program, handoff and feedback: the immutable contribution that
///     the handoff admitted, with its corrections, responses and resolution. It is materialized from the admission at
///     revision one and every later fact advances the logical revision by one; corrections preserve the original
///     contribution. The thread never loads its handoff, so revoking or closing the handoff never changes it.
/// </summary>
public sealed class AdvisorFeedbackThread : Aggregate
{
    /// <summary>Stream area of every feedback thread (the realm is the tenant).</summary>
    public const string Area = "advisor-feedback";

    /// <summary>The identity owner type of a feedback thread.</summary>
    public const string OwnerType = "AdvisorFeedbackThread";

    readonly Uuid _tenantId;
    readonly AdvisorReplayCut _cut = new();
    readonly Uuid _programId;
    readonly Uuid _handoffId;
    readonly Uuid _feedbackId;
    AdvisorFeedbackRecorded? _recorded;
    AdvisorFeedbackResolved? _resolved;
    long _revision;

    /// <summary>Opens one thread, selecting the logical operation to resolve.</summary>
    public AdvisorFeedbackThread(Uuid tenantId, Uuid programId, Uuid handoffId, Uuid feedbackId,
        Uuid selectedLogicalId = default)
        : base(IdentityOf(tenantId, programId, handoffId, feedbackId), new EventStreamAddress(tenantId.ToString(),
            Area, IdentityOf(tenantId, programId, handoffId, feedbackId).ToString()))
    {
        (_tenantId, _programId, _handoffId, _feedbackId) = (tenantId, programId, handoffId, feedbackId);
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<AdvisorFeedbackRecorded>(Apply);
        On<AdvisorFeedbackCorrected>(Apply);
        On<AdvisorFeedbackResponded>(Apply);
        On<AdvisorFeedbackResolved>(Apply);
    }

    /// <summary>The identity of the thread of a feedback on a handoff.</summary>
    public static Uuid IdentityOf(Uuid tenantId, Uuid programId, Uuid handoffId, Uuid feedbackId) =>
        AggregateIdentity.Create(OwnerType, tenantId, programId, handoffId, feedbackId);

    internal AdvisorReplayCut ReplayCut => _cut;

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The feedback identity.</summary>
    public Uuid FeedbackId => _feedbackId;

    /// <summary>The handoff the feedback was admitted on.</summary>
    public Uuid HandoffId => _handoffId;

    /// <summary>The logical revision: zero before materialization, one when recorded, plus one per later fact.</summary>
    public long Revision => _revision;

    /// <summary>True once the thread was materialized.</summary>
    public bool IsMaterialized => _recorded is not null;

    /// <summary>The materialization fact, once recorded.</summary>
    public AdvisorFeedbackRecorded? Recorded => _recorded;

    /// <summary>The resolution fact, once recorded.</summary>
    public AdvisorFeedbackResolved? Resolved => _resolved;

    public Uuid TenantId => _tenantId;
    public Uuid ProgramId => _programId;

    /// <summary>Checks that the thread can be materialized.</summary>
    public Result PrepareMaterialize() => _recorded is null
        ? Result.Success
        : Result.Failure(new RequestError(RequestErrorKind.Conflict, "The advisor feedback is already materialized."));

    /// <summary>Checks that the thread can take a write at the expected revision.</summary>
    public Result PrepareWrite(long expectedRevision)
    {
        if (_recorded is null)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The advisor feedback to change was not found."));
        return expectedRevision == _revision
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The advisor feedback revision is stale; the current revision is {_revision}."));
    }

    /// <summary>Checks that the thread can be resolved at the expected revision.</summary>
    public Result PrepareResolve(long expectedRevision)
    {
        var prepared = PrepareWrite(expectedRevision);
        if (!prepared.IsSuccess)
            return prepared;
        return _resolved is null
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict, "The advisor feedback is already resolved."));
    }

    /// <summary>Materializes the admitted contribution as a receiver fact at revision one.</summary>
    public Result Materialize(OperationProvenance operation, RecordAdvisorFeedback authored, ActorReference author,
        DateTimeOffset createdAt, ImmutableContentRef intent)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(authored);
        ArgumentNullException.ThrowIfNull(intent);
        var prepared = PrepareMaterialize();
        if (!prepared.IsSuccess)
            return prepared;
        if (operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A materialization is recorded on the thread's own stream."));
        var append = _cut.Require(this, Acceptance, operation);
        if (!append.IsSuccess)
            return append;
        RaiseEvent(new AdvisorFeedbackRecorded(_tenantId, _programId, _handoffId, _feedbackId, authored.RecordId,
            authored.RecordVersionId, authored.Kind, author, createdAt, operation, intent,
            null, null)
        {
            SelectedSource = authored.SelectedSource,
        });
        return Result.Success;
    }

    /// <summary>Appends a correction as the root native fact of its operation.</summary>
    public Result Correct(OperationProvenance operation, Uuid correctionId, ImmutableContentRef authored, ActorReference correctedBy, DateTimeOffset correctedAt,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var prepared = PrepareOwn(operation, PrepareWrite);
        if (!prepared.IsSuccess)
            return prepared;
        var append = _cut.Require(this, Acceptance, operation);
        if (!append.IsSuccess)
            return append;
        RaiseEvent(new AdvisorFeedbackCorrected(_tenantId, _programId, _handoffId, _feedbackId, correctionId,
            authored, correctedBy, correctedAt, operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Appends a client response as the root native fact of its operation.</summary>
    public Result Respond(OperationProvenance operation, ImmutableContentRef authored, ActorReference respondedBy,
        DateTimeOffset respondedAt, ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var prepared = PrepareOwn(operation, PrepareWrite);
        if (!prepared.IsSuccess)
            return prepared;
        var append = _cut.Require(this, Acceptance, operation);
        if (!append.IsSuccess)
            return append;
        RaiseEvent(new AdvisorFeedbackResponded(_tenantId, _programId, _handoffId, _feedbackId, authored,
            respondedBy, respondedAt, operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Resolves the thread as the root native fact of its operation.</summary>
    public Result Resolve(OperationProvenance operation, ActorReference resolvedBy, DateTimeOffset resolvedAt,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var prepared = PrepareOwn(operation, PrepareResolve);
        if (!prepared.IsSuccess)
            return prepared;
        var append = _cut.Require(this, Acceptance, operation);
        if (!append.IsSuccess)
            return append;
        RaiseEvent(new AdvisorFeedbackResolved(_tenantId, _programId, _handoffId, _feedbackId, resolvedBy,
            resolvedAt, operation, firstResponse, operationResult));
        return Result.Success;
    }

    protected override void Apply(DomainEvent ev)
    {
        _cut.Observe(ev);
        base.Apply(ev);
    }

    void Apply(AdvisorFeedbackRecorded ev)
    {
        if (ev.Operation is null)
            throw new ImmutableContentIntegrityException("The advisor native fact lacks operation provenance.");
        if (Acceptance.SelectedFact is not null && Acceptance.SelectedLogicalId == ev.Operation.LogicalId)
            throw new ImmutableContentIntegrityException("The selected advisor native operation occurs more than once.");
        Acceptance.Observe(ev);
        if (_recorded is not null || !InScope(ev.TenantId, ev.ProgramId, ev.HandoffId, ev.FeedbackId) ||
            ev.Operation.Owner != Stream)
            throw new ImmutableContentIntegrityException("A thread is materialized once, at revision one, on its own stream.");
        _recorded = ev;
        _revision = 1;
    }

    void Apply(AdvisorFeedbackCorrected ev)
    {
        if (ev.Operation is null)
            throw new ImmutableContentIntegrityException("The advisor native fact lacks operation provenance.");
        if (Acceptance.SelectedFact is not null && Acceptance.SelectedLogicalId == ev.Operation.LogicalId)
            throw new ImmutableContentIntegrityException("The selected advisor native operation occurs more than once.");
        Acceptance.Observe(ev);
        EnsureWritable(ev.TenantId, ev.ProgramId, ev.HandoffId, ev.FeedbackId, ev.Operation);
        Advance();
    }

    void Apply(AdvisorFeedbackResponded ev)
    {
        if (ev.Operation is null)
            throw new ImmutableContentIntegrityException("The advisor native fact lacks operation provenance.");
        if (Acceptance.SelectedFact is not null && Acceptance.SelectedLogicalId == ev.Operation.LogicalId)
            throw new ImmutableContentIntegrityException("The selected advisor native operation occurs more than once.");
        Acceptance.Observe(ev);
        EnsureWritable(ev.TenantId, ev.ProgramId, ev.HandoffId, ev.FeedbackId, ev.Operation);
        Advance();
    }

    void Apply(AdvisorFeedbackResolved ev)
    {
        if (ev.Operation is null)
            throw new ImmutableContentIntegrityException("The advisor native fact lacks operation provenance.");
        if (Acceptance.SelectedFact is not null && Acceptance.SelectedLogicalId == ev.Operation.LogicalId)
            throw new ImmutableContentIntegrityException("The selected advisor native operation occurs more than once.");
        Acceptance.Observe(ev);
        EnsureWritable(ev.TenantId, ev.ProgramId, ev.HandoffId, ev.FeedbackId, ev.Operation);
        if (_resolved is not null)
            throw new ImmutableContentIntegrityException("A thread is resolved once.");
        _resolved = ev;
        Advance();
    }

    bool InScope(Uuid tenantId, Uuid programId, Uuid handoffId, Uuid feedbackId) =>
        tenantId == _tenantId && programId == _programId && handoffId == _handoffId && feedbackId == _feedbackId;

    void EnsureWritable(Uuid tenantId, Uuid programId, Uuid handoffId, Uuid feedbackId, OperationProvenance operation)
    {
        if (_recorded is null || !InScope(tenantId, programId, handoffId, feedbackId) || operation.Owner != Stream)
            throw new ImmutableContentIntegrityException("A thread fact follows its materialization on its own stream.");
    }

    void Advance() => _revision++;

    Result PrepareOwn(OperationProvenance operation, Func<long, Result> prepare)
    {
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A thread write is a root operation of its own thread with an expected revision."));
        return prepare((long)expected);
    }
}
