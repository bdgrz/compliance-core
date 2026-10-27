using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NamedRecords;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.NamedRecords.Aggregates;

/// <summary>
///     The record owner of one provider, location or operational process. It accepts a complete change
///     (<c>*ChangeAccepted</c>), applies it once the name claims it needs are held, or cancels it after a definite
///     failure. At most one change is pending at a time; its generation, intent and expected revision are the fence
///     that makes a delayed apply or a late cancellation a terminal no-op. The owner keeps only references to the
///     contents of its last two applied revisions, so replay never grows with the record's history.
/// </summary>
public abstract class NamedRecord : Aggregate
{
    readonly string _noun;
    long _revision;
    long _generation;
    PendingChange? _pending;
    AppliedRecord? _current;
    AppliedRecord? _previous;

    /// <summary>Opens the record owner for the logical operation this hydration was selected for.</summary>
    protected NamedRecord(string noun, Uuid id, EventStreamAddress stream, Uuid selectedLogicalId) : base(id, stream)
    {
        _noun = noun;
        Acceptance = new NativeAcceptance(selectedLogicalId);
    }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>Logical revision: the number of changes applied so far; zero until the record is created.</summary>
    public long Revision => _revision;

    /// <summary>Number of changes accepted so far; the pending change's generation while one is pending.</summary>
    public long Generation => _generation;

    /// <summary>The accepted change that is not applied or canceled yet, if any.</summary>
    public PendingChange? Pending => _pending;

    /// <summary>The last applied revision, if any.</summary>
    public AppliedRecord? Current => _current;

    /// <summary>The applied revision <paramref name="revision" /> when it is one of the last two.</summary>
    public AppliedRecord? At(long revision) =>
        _current is { } current && current.Revision == revision ? current :
        _previous is { } previous && previous.Revision == revision ? previous : null;

    /// <summary>
    ///     Checks that a change can be accepted without raising anything. A creation needs an empty identity; a
    ///     revision needs the record at <paramref name="expectedRevision" />.
    /// </summary>
    public Result PrepareChange(bool create, long expectedRevision)
    {
        if (_pending is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                $"A change to this {_noun} is already pending; retry once it settles.", true));
        if (create)
            return _generation > 0
                ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    $"The {_noun} identity already exists.", false))
                : Result.Success;
        if (_revision == 0)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, $"The {_noun} was not found."));
        return expectedRevision == _revision
            ? Result.Success
            : Result.Failure(VersionedRecordRules.StaleRevision(_noun, _revision).ToRequestError());
    }

    /// <summary>
    ///     Accepts the change as the native fact of <paramref name="operation" />. Each concrete owner raises its own
    ///     typed fact after <see cref="CheckAccept" /> passed.
    /// </summary>
    public abstract Result AcceptChange(OperationProvenance operation, bool create, long expectedRevision,
        ImmutableContentRef intent, ImmutableContentRef firstResponse);

    /// <summary>Applies the pending change as the native fact of <paramref name="operation" />.</summary>
    public abstract Result ApplyChange(OperationProvenance operation, ImmutableContentRef intent,
        ImmutableContentRef content, ActorReference actor, DateTimeOffset changedAt, ImmutableContentRef result);

    /// <summary>Cancels the pending change as the native fact of <paramref name="operation" />, fencing its apply.</summary>
    public abstract Result CancelChange(OperationProvenance operation, ImmutableContentRef failureProof);

    /// <summary>True when the accepted change <paramref name="rootOperationId" /> at <paramref name="generation" /> is the pending one.</summary>
    public bool IsPending(Uuid rootOperationId, long generation) =>
        _pending is { } pending && pending.RootOperationId == rootOperationId && pending.Generation == generation;

    /// <summary>Checks an acceptance: its own logical operation and the guards of <see cref="PrepareChange" />.</summary>
    protected Result CheckAccept(OperationProvenance operation, bool create, long expectedRevision)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.LogicalId == Uuid.Empty || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                $"A {_noun} change requires its own logical operation."));
        return PrepareChange(create, expectedRevision);
    }

    /// <summary>Checks an application: its own logical operation and the exact pending change.</summary>
    protected Result CheckApply(OperationProvenance operation, ImmutableContentRef intent)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.LogicalId == Uuid.Empty || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                $"A {_noun} application requires its own logical operation."));
        return _pending is not { } pending || pending.RootOperationId != operation.RootOperationId ||
               pending.Intent != intent
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict, $"No such {_noun} change is pending.", false))
            : Result.Success;
    }

    /// <summary>Checks a cancellation: its own logical operation and the exact pending change.</summary>
    protected Result CheckCancel(OperationProvenance operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.LogicalId == Uuid.Empty || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                $"A {_noun} cancellation requires its own logical operation."));
        return _pending is not { } pending || pending.RootOperationId != operation.RootOperationId
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict, $"No such {_noun} change is pending.", false))
            : Result.Success;
    }

    /// <summary>The pending change a cancellation closes; call only after <see cref="CheckCancel" /> passed.</summary>
    protected PendingChange PendingForCancel => _pending
        ?? throw new InvalidOperationException("A cancellation needs a pending change.");

    /// <summary>Reducer of the accepted fact.</summary>
    protected void ObserveAccepted(INamedRecordFact fact)
    {
        Acceptance.Observe((DomainEvent)fact);
        if (fact.Owner != Stream || fact.Operation.Owner != Stream || fact.Operation.LogicalId == Uuid.Empty ||
            fact.Generation != _generation + 1 || _pending is not null || fact.Revision != _revision)
            throw new InvalidOperationException($"A {_noun} change must follow its record, generation and revision.");
        _generation = fact.Generation;
        _pending = new PendingChange(fact.Operation.RootOperationId, fact.Generation, fact.Revision, fact.Intent);
    }

    /// <summary>Reducer of the canceled fact.</summary>
    protected void ObserveCanceled(INamedRecordFact fact)
    {
        Acceptance.Observe((DomainEvent)fact);
        if (_pending is not { } pending || fact.Owner != Stream || fact.Operation.Owner != Stream ||
            pending.RootOperationId != fact.Operation.RootOperationId || pending.Generation != fact.Generation)
            throw new InvalidOperationException($"A {_noun} cancellation must close its pending change.");
        _pending = null;
    }

    /// <summary>Reducer of the applied fact.</summary>
    protected void ObserveApplied(INamedRecordAppliedFact fact)
    {
        Acceptance.Observe((DomainEvent)fact);
        if (_pending is not { } pending || fact.Operation.Owner != Stream ||
            pending.RootOperationId != fact.Operation.RootOperationId || pending.Intent != fact.Intent ||
            fact.RecordRevision != _revision + 1 || fact.RecordId != Id)
            throw new InvalidOperationException($"A {_noun} application must close its pending change.");
        _previous = _current;
        _current = new AppliedRecord(fact.RecordRevision, fact.ContentContent, fact.Actor, fact.ChangedAt);
        _revision = fact.RecordRevision;
        _pending = null;
    }

    /// <summary>An accepted change that has been neither applied nor canceled.</summary>
    public sealed record PendingChange(Uuid RootOperationId, long Generation, long ExpectedRevision,
        ImmutableContentRef Intent);

    /// <summary>One applied revision: its number, content reference, original actor and time.</summary>
    public sealed record AppliedRecord(long Revision, ImmutableContentRef Content, ActorReference Actor,
        DateTimeOffset ChangedAt);
}
