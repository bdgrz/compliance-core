using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AccessControl.Aggregates;

/// <summary>
///     One role's definition, name and deletion. Definition is accepted under the role id as its immutable creation
///     identity; a rename or deletion is accepted under its own command id. An exact retry replays the original
///     acceptance and the same identity with different content conflicts.
/// </summary>
public sealed class Role : Aggregate
{
    readonly Uuid _tenantId;
    bool _isDefined;
    bool _isDeleted;
    string? _name;

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The role already carries the requested name; nothing changes.</summary>
    public static RequestError NameUnchanged { get; } = new(RequestErrorKind.Conflict,
        "The role already has that name.", false);

    /// <summary>The role is already deleted; nothing changes.</summary>
    public static RequestError AlreadyDeleted { get; } = new(RequestErrorKind.Conflict,
        "The role is already deleted.", false);

    public bool IsActive => _isDefined && !_isDeleted;

    public Role(Uuid tenantId, Uuid roleId, Uuid selectedCommandId = default)
        : base(roleId, new EventStreamAddress(tenantId.ToString(), "rbac-roles", roleId.ToString()))
    {
        _tenantId = tenantId;
        Acceptance = new NativeAcceptance(selectedCommandId);
        On<RoleDefined>(ev =>
        {
            if (ev.TenantId != _tenantId || ev.RoleId != Id || _isDefined || _isDeleted || string.IsNullOrWhiteSpace(ev.Name) ||
                ev.CommandId != Id && !(BuiltInRbac.IsBuiltInRole(_tenantId, Id) && ev.Operation.CommandKind == nameof(RenameRole)))
                throw new InvalidOperationException("Role definition must preserve its owner, creation identity and lifecycle.");
            Accept(ev, ev.CommandId, ev.Operation);
            _isDefined = true;
            _name = ev.Name;
        });
        On<RoleRenamed>(ev =>
        {
            if (ev.TenantId != _tenantId || ev.RoleId != Id || !_isDefined || _isDeleted || string.IsNullOrWhiteSpace(ev.Name))
                throw new InvalidOperationException("Role rename requires the exact active role.");
            Accept(ev, ev.CommandId, ev.Operation);
            _name = ev.Name;
        });
        On<RoleDeleted>(ev =>
        {
            if (ev.TenantId != _tenantId || ev.RoleId != Id || !_isDefined || _isDeleted || BuiltInRbac.IsBuiltInRole(_tenantId, Id))
                throw new InvalidOperationException("Role deletion requires the exact active non-built-in role.");
            Accept(ev, ev.CommandId, ev.Operation);
            _isDeleted = true;
        });
    }

    /// <summary>Checks a definition against hydrated state without raising anything.</summary>
    public Result PrepareDefine(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "A role requires a name."));
        return _isDefined
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict, "The role is already defined."))
            : Result.Success;
    }

    /// <summary>Accepts the definition as the native fact of <paramref name="operation" />.</summary>
    public Result Define(OperationProvenance operation, ImmutableContentRef firstResponse, string name)
    {
        var prepared = Prepare(operation, firstResponse, PrepareDefine(name));
        if (!prepared.IsSuccess)
            return prepared;
        if (operation.LogicalId != Id)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "Role definition requires its role identity."));
        RaiseEvent(new RoleDefined(_tenantId, Id, name.Trim(), operation.LogicalId, operation, firstResponse));
        return Result.Success;
    }

    /// <summary>
    ///     Checks a rename against hydrated state without raising anything. A role that already has the name reports
    ///     <see cref="NameUnchanged" />, which dispatchers treat as a terminal no-op.
    /// </summary>
    public Result PrepareRename(string name)
    {
        if (_isDeleted || !_isDefined && !BuiltInRbac.IsBuiltInRole(_tenantId, Id))
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The role is not defined."));
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "A role requires a name."));
        return _isDefined && string.Equals(_name, name.Trim(), StringComparison.Ordinal)
            ? Result.Failure(NameUnchanged)
            : Result.Success;
    }

    /// <summary>Accepts the rename as the native fact of <paramref name="operation" />.</summary>
    public Result Rename(OperationProvenance operation, ImmutableContentRef firstResponse, string name)
    {
        var prepared = Prepare(operation, firstResponse, PrepareRename(name));
        if (!prepared.IsSuccess)
            return prepared;
        var normalized = name.Trim();
        // Renaming a built-in role the catalog has not defined yet defines it under the migrated name.
        RaiseEvent(_isDefined
            ? new RoleRenamed(_tenantId, Id, normalized, operation.LogicalId, operation, firstResponse)
            : new RoleDefined(_tenantId, Id, normalized, operation.LogicalId, operation, firstResponse));
        return Result.Success;
    }

    /// <summary>
    ///     Checks a deletion against hydrated state without raising anything. A role that is already deleted reports
    ///     <see cref="AlreadyDeleted" />, which dispatchers treat as a terminal no-op.
    /// </summary>
    public Result PrepareDelete()
    {
        if (BuiltInRbac.IsBuiltInRole(_tenantId, Id))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "Built-in roles cannot be deleted."));
        if (!_isDefined)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The role is not defined."));
        return _isDeleted ? Result.Failure(AlreadyDeleted) : Result.Success;
    }

    /// <summary>Accepts the deletion as the native fact of <paramref name="operation" />.</summary>
    public Result Delete(OperationProvenance operation, ImmutableContentRef firstResponse)
    {
        var prepared = Prepare(operation, firstResponse, PrepareDelete());
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new RoleDeleted(_tenantId, Id, operation.LogicalId, operation, firstResponse));
        return Result.Success;
    }

    Result Prepare(OperationProvenance operation, ImmutableContentRef firstResponse, Result prepared)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(firstResponse);
        if (operation.LogicalId == Uuid.Empty || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A role operation requires its own logical operation."));
        return prepared;
    }

    void Accept(DomainEvent fact, Uuid commandId, OperationProvenance operation)
    {
        OwnerProvenance.RequireOwn(this, commandId, operation);
        Acceptance.Observe(fact);
    }
}
