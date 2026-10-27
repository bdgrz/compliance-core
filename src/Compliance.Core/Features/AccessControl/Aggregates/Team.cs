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
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AccessControl.Aggregates;

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
            if (ev.TenantId != _tenantId || ev.TeamId != Id || ev.CommandId != Id || _isDefined || _isDeleted || string.IsNullOrWhiteSpace(ev.Name))
                throw new InvalidOperationException("Team definition must preserve its owner, creation identity and lifecycle.");
            Accept(ev, ev.CommandId, ev.Operation);
            _isDefined = true;
        });
        On<TeamDeleted>(ev =>
        {
            if (ev.TenantId != _tenantId || ev.TeamId != Id || !_isDefined || _isDeleted || BuiltInRbac.IsBuiltInTeam(_tenantId, Id))
                throw new InvalidOperationException("Team deletion requires the exact active non-built-in team.");
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
        if (operation.LogicalId != Id)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "Team definition requires its team identity."));
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
