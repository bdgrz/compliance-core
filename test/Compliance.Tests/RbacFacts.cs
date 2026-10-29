using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests;

/// <summary>Builds well-formed native role and team facts and drives the owners with fresh logical operations.</summary>
static class RbacFacts
{
    static readonly ActorReference Actor = ActorReference.ForSystemProcess("system", "system");
    static readonly DateTimeOffset At = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The provenance of an operation on <paramref name="owner" />.</summary>
    public static OperationProvenance Provenance(Aggregate owner, string commandKind, Uuid commandId,
        ActorReference? actor = null) =>
        OwnerProvenance.For(owner, commandId, commandKind, actor ?? Actor, "current", null, new string('a', 64), At);

    public static RoleDefined RoleDefinedFact(Uuid tenantId, Uuid roleId, string name)
    {
        var operation = Provenance(new Role(tenantId, roleId), "DefineRole", roleId);
        return new RoleDefined(tenantId, roleId, name, roleId, operation, TenantFacts.Response());
    }

    public static RoleRenamed RoleRenamedFact(Uuid tenantId, Uuid roleId, string name)
    {
        var operation = Provenance(new Role(tenantId, roleId), "RenameRole", Uuid.CreateVersion4());
        return new RoleRenamed(tenantId, roleId, name, operation.LogicalId, operation, TenantFacts.Response());
    }

    public static RoleDeleted RoleDeletedFact(Uuid tenantId, Uuid roleId)
    {
        var operation = Provenance(new Role(tenantId, roleId), "DeleteRole", Uuid.CreateVersion4());
        return new RoleDeleted(tenantId, roleId, operation.LogicalId, operation, TenantFacts.Response());
    }

    public static TeamDefined TeamDefinedFact(Uuid tenantId, Uuid teamId, string name)
    {
        var operation = Provenance(new Team(tenantId, teamId), "DefineTeam", teamId);
        return new TeamDefined(tenantId, teamId, name, teamId, operation, TenantFacts.Response());
    }

    public static TeamDeleted TeamDeletedFact(Uuid tenantId, Uuid teamId)
    {
        var operation = Provenance(new Team(tenantId, teamId), "DeleteTeam", Uuid.CreateVersion4());
        return new TeamDeleted(tenantId, teamId, operation.LogicalId, operation, TenantFacts.Response());
    }

    /// <summary>Defines the role under its own id, the creation identity the handler uses.</summary>
    public static Result Define(this Role role, string name) =>
        role.Define(Provenance(role, "DefineRole", role.Id), TenantFacts.Response(), name);

    public static Result Rename(this Role role, string name) =>
        role.Rename(Provenance(role, "RenameRole", Uuid.CreateVersion4()), TenantFacts.Response(), name);

    public static Result Delete(this Role role) =>
        role.Delete(Provenance(role, "DeleteRole", Uuid.CreateVersion4()), TenantFacts.Response());

    /// <summary>Defines the team under its own id, the creation identity the handler uses.</summary>
    public static Result Define(this Team team, string name) =>
        team.Define(Provenance(team, "DefineTeam", team.Id), TenantFacts.Response(), name);

    public static Result Delete(this Team team) =>
        team.Delete(Provenance(team, "DeleteTeam", Uuid.CreateVersion4()), TenantFacts.Response());
}
