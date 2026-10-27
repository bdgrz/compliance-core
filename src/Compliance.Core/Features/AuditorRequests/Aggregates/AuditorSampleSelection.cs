using Bdgrz.Compliance.Features.AuditorRequests.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests.Aggregates;

/// <summary>
///     One exact sample selection, identified by its request and selection. It is materialized from the request's
///     admission at revision one and is otherwise immutable: an amendment is a new selection that names the one it
///     supersedes. The selection never loads the live request or the latest population, so a request that resolved before
///     it materialized does not lose it.
/// </summary>
public sealed class AuditorSampleSelection : Aggregate
{
    /// <summary>Stream area of every auditor sample selection (the realm is the tenant).</summary>
    public const string Area = "auditor-sample-selections";

    /// <summary>The identity owner type of an auditor sample selection.</summary>
    public const string OwnerType = "AuditorSampleSelection";

    readonly Uuid _tenantId;
    readonly Uuid _requestId;
    readonly Uuid _selectionId;
    SampleSelectionRecorded? _recorded;

    /// <summary>Opens one selection, selecting the logical operation to resolve.</summary>
    public AuditorSampleSelection(Uuid tenantId, Uuid programId, Uuid requestId, Uuid selectionId,
        Uuid selectedLogicalId = default)
        : base(IdentityOf(tenantId, programId, requestId, selectionId),
            new EventStreamAddress(tenantId.ToString(), Area,
                IdentityOf(tenantId, programId, requestId, selectionId).ToString()))
    {
        (_tenantId, _requestId, _selectionId) = (tenantId, requestId, selectionId);
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<SampleSelectionRecorded>(Apply);
    }

    /// <summary>The identity of the selection of a request.</summary>
    public static Uuid IdentityOf(Uuid tenantId, Uuid programId, Uuid requestId, Uuid selectionId) =>
        AggregateIdentity.Create(OwnerType, tenantId, programId, requestId, selectionId);

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The request this selection belongs to.</summary>
    public Uuid RequestId => _requestId;

    /// <summary>The selection identity.</summary>
    public Uuid SelectionId => _selectionId;

    /// <summary>The logical revision: zero before materialization, one afterwards.</summary>
    public long Revision => _recorded is null ? 0 : 1;

    /// <summary>True once the selection was materialized.</summary>
    public bool IsMaterialized => _recorded is not null;

    /// <summary>The materialization fact, once recorded.</summary>
    public SampleSelectionRecorded? Recorded => _recorded;

    /// <summary>Checks that the selection can be materialized.</summary>
    public Result PrepareMaterialize() => _recorded is null
        ? Result.Success
        : Result.Failure(new RequestError(RequestErrorKind.Conflict,
            "The sample selection is already materialized."));

    /// <summary>Materializes the admitted selection as a receiver fact at revision one.</summary>
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
                "A materialization is recorded on the selection's own stream."));
        RaiseEvent(new SampleSelectionRecorded(_tenantId, _selectionId, 1, actorMemberId, intent, at, operation, null,
            operationResult));
        return Result.Success;
    }

    void Apply(SampleSelectionRecorded ev)
    {
        Acceptance.Observe(ev);
        if (_recorded is not null || ev.TenantId != _tenantId || ev.SubjectId != _selectionId || ev.Revision != 1 ||
            ev.Operation.Owner != Stream)
            throw new InvalidOperationException("A selection is materialized once, at revision one, on its own stream.");
        _recorded = ev;
    }
}
