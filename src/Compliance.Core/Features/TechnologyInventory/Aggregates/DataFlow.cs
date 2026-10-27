using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>
///     Owns one manually inventoried data flow and its attributed versions. Every version is the native fact of its
///     own logical operation, so exact retries are decided by the original operation and never by the flow's current
///     content. The carried-asset list is complete and unbounded.
/// </summary>
public sealed class DataFlow : Aggregate
{
    readonly Uuid _tenantId;
    bool _created;
    bool _foreign;
    long _revision;
    DataFlowContent? _content;

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    public bool IsCreated => _created && !_foreign;
    public long Revision => _revision;
    public DataFlowContent? Content => IsCreated ? _content : null;

    public DataFlow(Uuid tenantId, Uuid id, Uuid selectedLogicalId = default)
        : base(id, TechnologyInventoryStreams.Address(tenantId, id))
    {
        _tenantId = tenantId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<DataFlowRevisionRecorded>(ev =>
        {
            Acceptance.Observe(ev);
            _created = true;
            _revision = ev.Revision;
            _content = ev.Content;
        });
        On<TechnologyComponentRevisionRecorded>(_ => _foreign = true);
        On<InformationAssetRevisionRecorded>(_ => _foreign = true);
    }

    /// <summary>Checks a creation against hydrated state without raising anything; returns the normalized content.</summary>
    public Result<DataFlowContent> PrepareRecord(DataFlowContent requested)
    {
        var content = TechnologyInventoryRules.Normalize(requested with
        {
            Lifecycle = TechnologyInventoryRules.Active,
        });
        var error = TechnologyInventoryRules.Validate(content);
        if (error is not null)
            return Result<DataFlowContent>.Failure(new RequestError(RequestErrorKind.Validation, error));
        if (_foreign)
            return Result<DataFlowContent>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The identity belongs to another inventory record."));
        return _created
            ? Result<DataFlowContent>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The data flow already exists."))
            : Result<DataFlowContent>.Success(content);
    }

    /// <summary>Accepts the creation as the native fact of <paramref name="operation" />.</summary>
    public Result Record(OperationProvenance operation, ImmutableContentRef firstResponse,
        DataFlowContent requested, ActorReference actor, DateTimeOffset changedAt)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(firstResponse);
        if (operation.LogicalId == Uuid.Empty || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A data flow creation requires its own logical operation."));
        var prepared = PrepareRecord(requested);
        if (!prepared.IsSuccess)
            return Result.Failure(prepared.Error!);
        RaiseEvent(new DataFlowRevisionRecorded(_tenantId, Id, 1, prepared.Value!, actor, changedAt, operation,
            firstResponse));
        return Result.Success;
    }

    /// <summary>Checks a revision against hydrated state without raising anything; returns the normalized content.</summary>
    public Result<DataFlowContent> PrepareRevise(long expectedRevision,
        Func<DataFlowContent, DataFlowContent> change)
    {
        if (!IsCreated)
            return Failure(CommandFailure.MissingRecord("The data flow was not found."));
        if (expectedRevision != _revision)
            return Failure(CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("data flow", _revision)));
        var content = TechnologyInventoryRules.Normalize(change(_content!));
        var error = TechnologyInventoryRules.Validate(content) ??
                    (content.EffectiveFrom < _content!.EffectiveFrom
                        ? "A data flow version cannot take effect before its predecessor."
                        : null);
        return error is not null
            ? Failure(CommandFailure.InvalidContent(error))
            : Result<DataFlowContent>.Success(content);
    }

    /// <summary>Accepts the revision as the native fact of <paramref name="operation" />.</summary>
    public Result Revise(OperationProvenance operation, ImmutableContentRef firstResponse, long expectedRevision,
        Func<DataFlowContent, DataFlowContent> change, ActorReference actor, DateTimeOffset changedAt)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(firstResponse);
        if (operation.LogicalId == Uuid.Empty || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A data flow revision requires its own logical operation."));
        var prepared = PrepareRevise(expectedRevision, change);
        if (!prepared.IsSuccess)
            return Result.Failure(prepared.Error!);
        RaiseEvent(new DataFlowRevisionRecorded(_tenantId, Id, _revision + 1, prepared.Value!, actor, changedAt,
            operation, firstResponse));
        return Result.Success;
    }

    static Result<DataFlowContent> Failure(CommandFailure failure) =>
        Result<DataFlowContent>.Failure(failure.ToRequestError());
}
