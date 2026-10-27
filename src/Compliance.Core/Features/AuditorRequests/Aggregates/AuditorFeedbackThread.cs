using Bdgrz.Compliance.Features.AuditorRequests.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests.Aggregates;

/// <summary>
///     The thread of one comment, identified by its request and comment (thread) id. It is materialized from the
///     request's admission at revision one and is otherwise immutable; no reply policy is introduced. The thread never
///     loads the live request, so a later resolution cannot discard an admitted comment.
/// </summary>
public sealed class AuditorFeedbackThread : Aggregate
{
    /// <summary>Stream area of every auditor feedback thread (the realm is the tenant).</summary>
    public const string Area = "auditor-feedback-threads";

    /// <summary>The identity owner type of an auditor feedback thread.</summary>
    public const string OwnerType = "AuditorFeedbackThread";

    readonly Uuid _tenantId;
    readonly Uuid _requestId;
    readonly Uuid _threadId;
    AuditorRequestCommentRecorded? _recorded;

    /// <summary>Opens one thread, selecting the logical operation to resolve.</summary>
    public AuditorFeedbackThread(Uuid tenantId, Uuid programId, Uuid requestId, Uuid threadId,
        Uuid selectedLogicalId = default)
        : base(IdentityOf(tenantId, programId, requestId, threadId),
            new EventStreamAddress(tenantId.ToString(), Area,
                IdentityOf(tenantId, programId, requestId, threadId).ToString()))
    {
        (_tenantId, _requestId, _threadId) = (tenantId, requestId, threadId);
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<AuditorRequestCommentRecorded>(Apply);
    }

    /// <summary>The identity of the thread of a comment.</summary>
    public static Uuid IdentityOf(Uuid tenantId, Uuid programId, Uuid requestId, Uuid threadId) =>
        AggregateIdentity.Create(OwnerType, tenantId, programId, requestId, threadId);

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The request this thread belongs to.</summary>
    public Uuid RequestId => _requestId;

    /// <summary>The thread identity, which is the comment id.</summary>
    public Uuid ThreadId => _threadId;

    /// <summary>The logical revision: zero before materialization, one afterwards.</summary>
    public long Revision => _recorded is null ? 0 : 1;

    /// <summary>True once the thread was materialized.</summary>
    public bool IsMaterialized => _recorded is not null;

    /// <summary>The materialization fact, once recorded.</summary>
    public AuditorRequestCommentRecorded? Recorded => _recorded;

    /// <summary>Checks that the thread can be materialized.</summary>
    public Result PrepareMaterialize() => _recorded is null
        ? Result.Success
        : Result.Failure(new RequestError(RequestErrorKind.Conflict, "The comment thread is already materialized."));

    /// <summary>Materializes the admitted comment as a receiver fact at revision one.</summary>
    public Result Materialize(OperationProvenance operation, ImmutableContentRef intent, Uuid actorMemberId,
        DateTimeOffset at, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        var prepared = PrepareMaterialize();
        if (!prepared.IsSuccess)
            return prepared;
        if (operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A materialization is recorded on the thread's own stream."));
        RaiseEvent(new AuditorRequestCommentRecorded(_tenantId, _threadId, 1, actorMemberId, intent, at, operation, null,
            operationResult));
        return Result.Success;
    }

    void Apply(AuditorRequestCommentRecorded ev)
    {
        Acceptance.Observe(ev);
        if (_recorded is not null || ev.TenantId != _tenantId || ev.SubjectId != _threadId || ev.Revision != 1 ||
            ev.Operation.Owner != Stream)
            throw new InvalidOperationException("A thread is materialized once, at revision one, on its own stream.");
        _recorded = ev;
    }
}
