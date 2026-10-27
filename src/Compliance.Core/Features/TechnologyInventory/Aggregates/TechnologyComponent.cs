using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>
///     Owns one manually inventoried technology component and its attributed revisions. Every revision is the native
///     fact of its own logical operation, so exact retries are decided by the original operation and never by the
///     component's current content.
/// </summary>
public sealed class TechnologyComponent : Aggregate
{
    readonly Uuid _tenantId;
    bool _created;
    bool _foreign;
    long _revision;
    TechnologyComponentContent? _content;

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    public bool IsCreated => _created && !_foreign;
    public long Revision => _revision;
    public TechnologyComponentContent? Content => IsCreated ? _content : null;

    public TechnologyComponent(Uuid tenantId, Uuid id, Uuid selectedLogicalId = default)
        : base(id, TechnologyInventoryStreams.Address(tenantId, id))
    {
        _tenantId = tenantId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<TechnologyComponentRevisionRecorded>(ev =>
        {
            Acceptance.Observe(ev);
            _created = true;
            _revision = ev.Revision;
            _content = ev.Content;
        });
        On<InformationAssetRevisionRecorded>(_ => _foreign = true);
        On<DataFlowRevisionRecorded>(_ => _foreign = true);
    }

    /// <summary>Checks a creation against hydrated state without raising anything; returns the normalized content.</summary>
    public Result<TechnologyComponentContent> PrepareRecord(TechnologyComponentContent requested)
    {
        var content = TechnologyInventoryRules.Normalize(requested with
        {
            Lifecycle = TechnologyInventoryRules.Active,
        });
        var error = TechnologyInventoryRules.Validate(content) ??
                    (content.Category == TechnologyInventoryRules.CloudAccount &&
                     content.SystemInstanceId != Id
                        ? "A cloud_account component shares its system instance identity."
                        : null);
        if (error is not null)
            return Result<TechnologyComponentContent>.Failure(new RequestError(RequestErrorKind.Validation, error));
        if (_foreign)
            return Result<TechnologyComponentContent>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The identity belongs to another inventory record."));
        return _created
            ? Result<TechnologyComponentContent>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The technology component already exists."))
            : Result<TechnologyComponentContent>.Success(content);
    }

    /// <summary>Accepts the creation as the native fact of <paramref name="operation" />.</summary>
    public Result Record(OperationProvenance operation, ImmutableContentRef firstResponse,
        TechnologyComponentContent requested, ActorReference actor, DateTimeOffset changedAt)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(firstResponse);
        if (operation.LogicalId == Uuid.Empty || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A component creation requires its own logical operation."));
        var prepared = PrepareRecord(requested);
        if (!prepared.IsSuccess)
            return Result.Failure(prepared.Error!);
        RaiseEvent(new TechnologyComponentRevisionRecorded(_tenantId, Id, 1, prepared.Value!, actor, changedAt,
            operation, firstResponse));
        return Result.Success;
    }

    /// <summary>Checks a revision against hydrated state without raising anything; returns the normalized content.</summary>
    public Result<TechnologyComponentContent> PrepareRevise(long expectedRevision,
        Func<TechnologyComponentContent, TechnologyComponentContent> change)
    {
        if (!IsCreated)
            return Failure(CommandFailure.MissingRecord("The technology component was not found."));
        if (expectedRevision != _revision)
            return Failure(CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("technology component",
                _revision)));
        var content = TechnologyInventoryRules.Normalize(change(_content!));
        var error = TechnologyInventoryRules.Validate(content) ??
                    (content.Category != _content!.Category ||
                     content.SystemInstanceId != _content.SystemInstanceId
                        ? "A component's category and system instance are immutable."
                        : null);
        return error is not null
            ? Failure(CommandFailure.InvalidContent(error))
            : Result<TechnologyComponentContent>.Success(content);
    }

    /// <summary>Accepts the revision as the native fact of <paramref name="operation" />.</summary>
    public Result Revise(OperationProvenance operation, ImmutableContentRef firstResponse, long expectedRevision,
        Func<TechnologyComponentContent, TechnologyComponentContent> change, ActorReference actor,
        DateTimeOffset changedAt)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(firstResponse);
        if (operation.LogicalId == Uuid.Empty || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A component revision requires its own logical operation."));
        var prepared = PrepareRevise(expectedRevision, change);
        if (!prepared.IsSuccess)
            return Result.Failure(prepared.Error!);
        RaiseEvent(new TechnologyComponentRevisionRecorded(_tenantId, Id, _revision + 1, prepared.Value!, actor,
            changedAt, operation, firstResponse));
        return Result.Success;
    }

    static Result<TechnologyComponentContent> Failure(CommandFailure failure) =>
        Result<TechnologyComponentContent>.Failure(failure.ToRequestError());
}
