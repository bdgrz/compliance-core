using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     One team's definition and deletion. Definition is accepted under the team id as its immutable creation
///     identity and deletion under its own command id. An exact retry replays the original acceptance and the same
///     identity with different content conflicts.
/// </summary>
public sealed class Team : Aggregate
{
    readonly Uuid _tenantId;
    bool _isDefined;
    bool _isDeleted;

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The team is already deleted; nothing changes.</summary>
    public static RequestError AlreadyDeleted { get; } = new(RequestErrorKind.Conflict,
        "The team is already deleted.", false);

    public bool IsActive => _isDefined && !_isDeleted;

    public Team(Uuid tenantId, Uuid teamId, Uuid selectedCommandId = default)
        : base(teamId, new EventStreamAddress(tenantId.ToString(), "rbac-teams", teamId.ToString()))
    {
        _tenantId = tenantId;
        Acceptance = new NativeAcceptance(selectedCommandId);
        On<TeamDefined>(ev =>
        {
            Accept(ev, ev.CommandId, ev.Operation);
            _isDefined = true;
        });
        On<TeamDeleted>(ev =>
        {
            Accept(ev, ev.CommandId, ev.Operation);
            _isDeleted = true;
        });
    }

    /// <summary>Checks a definition against hydrated state without raising anything.</summary>
    public Result PrepareDefine(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "A team requires a name."));
        return _isDefined
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict, "The team is already defined."))
            : Result.Success;
    }

    /// <summary>Accepts the definition as the native fact of <paramref name="operation" />.</summary>
    public Result Define(OperationProvenance operation, ImmutableContentRef firstResponse, string name)
    {
        var prepared = Prepare(operation, firstResponse, PrepareDefine(name));
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new TeamDefined(_tenantId, Id, name.Trim(), operation.LogicalId, operation, firstResponse));
        return Result.Success;
    }

    /// <summary>
    ///     Checks a deletion against hydrated state without raising anything. A team that is already deleted reports
    ///     <see cref="AlreadyDeleted" />, which dispatchers treat as a terminal no-op.
    /// </summary>
    public Result PrepareDelete()
    {
        if (BuiltInRbac.IsBuiltInTeam(_tenantId, Id))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "Built-in teams cannot be deleted."));
        if (!_isDefined)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The team is not defined."));
        return _isDeleted ? Result.Failure(AlreadyDeleted) : Result.Success;
    }

    /// <summary>Accepts the deletion as the native fact of <paramref name="operation" />.</summary>
    public Result Delete(OperationProvenance operation, ImmutableContentRef firstResponse)
    {
        var prepared = Prepare(operation, firstResponse, PrepareDelete());
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new TeamDeleted(_tenantId, Id, operation.LogicalId, operation, firstResponse));
        return Result.Success;
    }

    Result Prepare(OperationProvenance operation, ImmutableContentRef firstResponse, Result prepared)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(firstResponse);
        if (operation.LogicalId == Uuid.Empty || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A team operation requires its own logical operation."));
        return prepared;
    }

    void Accept(DomainEvent fact, Uuid commandId, OperationProvenance operation)
    {
        OwnerProvenance.RequireOwn(this, commandId, operation);
        Acceptance.Observe(fact);
    }
}
