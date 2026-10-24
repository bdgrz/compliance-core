using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

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
    readonly Uuid _programId;
    readonly Uuid _handoffId;
    readonly Uuid _feedbackId;
    readonly List<AdvisorFeedbackCorrected> _corrections = [];
    readonly List<AdvisorFeedbackResponded> _responses = [];
    readonly List<DomainEvent> _facts = [];
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

    /// <summary>The corrections in order; the original contribution is never replaced.</summary>
    public IReadOnlyList<AdvisorFeedbackCorrected> Corrections => _corrections;

    /// <summary>The client responses in order.</summary>
    public IReadOnlyList<AdvisorFeedbackResponded> Responses => _responses;

    /// <summary>The resolution fact, once recorded.</summary>
    public AdvisorFeedbackResolved? Resolved => _resolved;

    /// <summary>The fact with this event id.</summary>
    public DomainEvent? FactById(Uuid eventId) => _facts.FirstOrDefault(fact => fact.Metadata?.EventId == eventId);

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
        RaiseEvent(new AdvisorFeedbackRecorded(_tenantId, _programId, _handoffId, _feedbackId, authored.RecordId,
            authored.RecordVersionId, authored.Kind, authored.Body.Trim(), author, createdAt, operation, intent,
            null, null)
        {
            SelectedSource = authored.SelectedSource,
        });
        return Result.Success;
    }

    /// <summary>Appends a correction as the root native fact of its operation.</summary>
    public Result Correct(OperationProvenance operation, Uuid correctionId, string correctedBody,
        string correctionReason, ActorReference correctedBy, DateTimeOffset correctedAt,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var prepared = PrepareOwn(operation, PrepareWrite);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new AdvisorFeedbackCorrected(_tenantId, _programId, _handoffId, _feedbackId, correctionId,
            correctedBody, correctionReason, correctedBy, correctedAt, operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Appends a client response as the root native fact of its operation.</summary>
    public Result Respond(OperationProvenance operation, string response, ActorReference respondedBy,
        DateTimeOffset respondedAt, ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var prepared = PrepareOwn(operation, PrepareWrite);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new AdvisorFeedbackResponded(_tenantId, _programId, _handoffId, _feedbackId, response,
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
        RaiseEvent(new AdvisorFeedbackResolved(_tenantId, _programId, _handoffId, _feedbackId, resolvedBy,
            resolvedAt, operation, firstResponse, operationResult));
        return Result.Success;
    }

    void Apply(AdvisorFeedbackRecorded ev)
    {
        Acceptance.Observe(ev);
        if (_recorded is not null || !InScope(ev.TenantId, ev.ProgramId, ev.HandoffId, ev.FeedbackId) ||
            ev.Operation.Owner != Stream)
            throw new InvalidOperationException("A thread is materialized once, at revision one, on its own stream.");
        _recorded = ev;
        _revision = 1;
        _facts.Add(ev);
    }

    void Apply(AdvisorFeedbackCorrected ev)
    {
        Acceptance.Observe(ev);
        EnsureWritable(ev.TenantId, ev.ProgramId, ev.HandoffId, ev.FeedbackId, ev.Operation);
        _corrections.Add(ev);
        Advance(ev);
    }

    void Apply(AdvisorFeedbackResponded ev)
    {
        Acceptance.Observe(ev);
        EnsureWritable(ev.TenantId, ev.ProgramId, ev.HandoffId, ev.FeedbackId, ev.Operation);
        _responses.Add(ev);
        Advance(ev);
    }

    void Apply(AdvisorFeedbackResolved ev)
    {
        Acceptance.Observe(ev);
        EnsureWritable(ev.TenantId, ev.ProgramId, ev.HandoffId, ev.FeedbackId, ev.Operation);
        if (_resolved is not null)
            throw new InvalidOperationException("A thread is resolved once.");
        _resolved = ev;
        Advance(ev);
    }

    bool InScope(Uuid tenantId, Uuid programId, Uuid handoffId, Uuid feedbackId) =>
        tenantId == _tenantId && programId == _programId && handoffId == _handoffId && feedbackId == _feedbackId;

    void EnsureWritable(Uuid tenantId, Uuid programId, Uuid handoffId, Uuid feedbackId, OperationProvenance operation)
    {
        if (_recorded is null || !InScope(tenantId, programId, handoffId, feedbackId) || operation.Owner != Stream)
            throw new InvalidOperationException("A thread fact follows its materialization on its own stream.");
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
                "A thread write is a root operation of its own thread with an expected revision."));
        return prepare((long)expected);
    }
}
