using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Evidence.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Tenants.Readers;
namespace Bdgrz.Compliance.Features.AccessControl.Rules;

sealed class AccessGrantProposalValidator(ITenantMembershipDirectoryReader memberships,
    ITeamDirectoryReader teams, IRoleDirectoryReader roles,
    IProgramResourceScopeResolver resourceScopes, IAggregateReader reader,
    IApplicationDirectoryReader? applications = null, IDomainEventReader? events = null)
    : IAccessGrantProposalValidator
{
    public async ValueTask<Result> ValidateAsync(Uuid tenantId, AccessGrantProposal proposal,
        CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || proposal is null || proposal.Principal is null ||
            proposal.Scope is null || proposal.Source is null || !Enum.IsDefined(proposal.Scope.Kind))
            return Invalid("The access grant proposal is incomplete.");
        if (proposal.Scope.ResourceType is not null &&
            proposal.Scope.Kind != AccessGrantScopeKind.SharedResource)
            return Invalid("Only a shared-resource scope may specify a resource type.");
        if (proposal.Scope.Kind == AccessGrantScopeKind.SharedResource &&
            proposal.Scope.ResourceType is not (TechnologyInventoryResourceTypes.InformationAsset or
                TechnologyInventoryResourceTypes.DataFlow or EvidenceArtifactResourceTypes.Artifact))
            return Invalid("This shared-resource access grant type is not supported.");

        var role = await roles.GetAsync(tenantId, proposal.RoleId, ct).ConfigureAwait(false);
        if (role is null || role.RoleId != proposal.RoleId)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The access grant role was not found in this organization."));
        var sourceRole = await reader.HydrateAsync(new Role(tenantId, proposal.RoleId), ct)
            .ConfigureAwait(false);
        if (!sourceRole.IsActive)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The access grant role was not found in this organization."));

        var principal = await ValidatePrincipalAsync(tenantId, proposal.Principal, proposal, ct)
            .ConfigureAwait(false);
        if (!principal.IsSuccess)
            return principal;

        if (proposal.Scope.Kind == AccessGrantScopeKind.Organization && proposal.Scope.Id != tenantId)
            return Invalid("The organization grant scope must match the request organization.");

        if (proposal.Scope.Kind == AccessGrantScopeKind.Program)
        {
            if (!await resourceScopes.IsTenantProgramAsync(tenantId, proposal.Scope.Id, ct)
                    .ConfigureAwait(false))
                return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The program scope was not found in this organization."));
        }
        else if (proposal.Scope.Kind == AccessGrantScopeKind.Application)
        {
            var application = await reader.HydrateApplicationAsync(tenantId, proposal.Scope.Id, ct).ConfigureAwait(false);
            if (!application.IsCreated)
                return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The application scope was not found in this organization."));
        }
        else if (proposal.Scope.Kind == AccessGrantScopeKind.SystemInstance)
        {
            var instance = await reader.HydrateAsync(new DeclaredSystemInstance(tenantId,
                proposal.Scope.Id), ct).ConfigureAwait(false);
            if (!instance.IsCreated &&
                !await IsLegacySystemInstanceAsync(tenantId, proposal.Scope.Id, ct)
                    .ConfigureAwait(false))
                return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The SystemInstance scope was not found in this organization."));
        }
        else if (proposal.Scope.Kind == AccessGrantScopeKind.SharedResource)
        {
            var exists = proposal.Scope.ResourceType switch
            {
                TechnologyInventoryResourceTypes.InformationAsset =>
                    (await reader.HydrateAsync(new InformationAsset(tenantId, proposal.Scope.Id), ct)
                        .ConfigureAwait(false)).IsCreated,
                TechnologyInventoryResourceTypes.DataFlow =>
                    (await reader.HydrateAsync(new DataFlow(tenantId, proposal.Scope.Id), ct)
                        .ConfigureAwait(false)).IsCreated,
                EvidenceArtifactResourceTypes.Artifact => events is not null &&
                    (await EvidenceArtifactMetadataRead.CaptureAsync(reader, events, tenantId, proposal.Scope.Id, ct)
                        .ConfigureAwait(false)).IsSuccess,
                _ => false,
            };
            if (!exists)
                return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                    proposal.Scope.ResourceType == EvidenceArtifactResourceTypes.Artifact
                        ? "The evidence artifact was not found in this organization."
                        : "The shared technology inventory resource was not found in this organization."));
        }
        else if (proposal.Scope.Kind != AccessGrantScopeKind.Organization)
            return Invalid("This access grant scope is not supported.");

        return Result.Success;
    }

    async ValueTask<bool> IsLegacySystemInstanceAsync(Uuid tenantId, Uuid instanceId,
        CancellationToken ct)
    {
        if (applications is null || events is null)
            return false;
        var projected = await applications.GetInstanceAsync(tenantId, instanceId, ct)
            .ConfigureAwait(false);
        if (projected is { } instance)
        {
            if (instance.TenantId != tenantId || instance.SystemInstanceId != instanceId ||
                instance.LegacyApplicationRevision is null ||
                !(await reader.HydrateApplicationAsync(tenantId, instance.ApplicationId, ct).ConfigureAwait(false)).IsCreated)
                return false;
            return await ScopedSystemInstanceSource.FindAsync(reader, events, tenantId,
                instance.ApplicationId, instanceId, ct).ConfigureAwait(false) is not null;
        }

        var pending = await new LegacySystemInstanceSource(applications, events)
            .FindPendingAsync(tenantId, instanceId, ct).ConfigureAwait(false);
        if (pending.Match is not SystemInstanceDeclared declaration)
            return false;
        return (await reader.HydrateApplicationAsync(tenantId, declaration.ApplicationId, ct).ConfigureAwait(false)).IsCreated;
    }

    async ValueTask<Result> ValidatePrincipalAsync(Uuid tenantId, AccessGrantPrincipal principal,
        AccessGrantProposal proposal, CancellationToken ct)
    {
        if (!Enum.IsDefined(principal.Kind) || principal.Id == Uuid.Empty)
            return Invalid("The access grant principal is invalid.");

        if (principal.Kind == AccessGrantPrincipalKind.Team)
        {
            var team = await teams.GetAsync(tenantId, principal.Id, ct).ConfigureAwait(false);
            var sourceTeam = team is not null && team.TeamId == principal.Id
                ? await reader.HydrateAsync(new Team(tenantId, principal.Id), ct)
                    .ConfigureAwait(false)
                : null;
            return sourceTeam is { IsActive: true }
                ? Result.Success
                : Result.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The access grant team was not found in this organization."));
        }

        string? cursor = null;
        do
        {
            var page = await memberships.ListAsync(tenantId, 100, cursor, ct).ConfigureAwait(false);
            foreach (var membership in page.Items)
            {
                if (membership.TenantId != tenantId ||
                    RbacIds.Member(tenantId, membership.UserId) != principal.Id)
                    continue;
                if (membership.IsDeprovisioned)
                    return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                        "The access grant member was not found in this organization."));
                if (membership.Affiliation == "client_personnel")
                    return Result.Success;
                if (membership.Affiliation == "guest")
                {
                    var sourceMember = await reader.HydrateAsync(
                        new Member(tenantId, membership.UserId), ct).ConfigureAwait(false);
                    var maximumUntil = sourceMember.MembershipExpiresOn is { } expiresOn &&
                                       expiresOn != DateOnly.MaxValue
                        ? new DateTimeOffset(expiresOn.AddDays(1).ToDateTime(TimeOnly.MinValue,
                            DateTimeKind.Utc))
                        : (DateTimeOffset?)null;
                    if (sourceMember.IsRegistered && sourceMember.Affiliation == "guest" &&
                        proposal.RoleId == BuiltInRbac.ViewerRoleId(tenantId) &&
                        proposal.Scope.Kind == AccessGrantScopeKind.Program &&
                        proposal.EffectiveUntil is { } effectiveUntil && maximumUntil is { } max &&
                        effectiveUntil <= max)
                        return Result.Success;
                }
                return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                    membership.Affiliation == "firm_staff"
                        ? "Firm staff require an accepted engagement assignment for client access."
                        : "Guest membership grants must be time-limited Viewer access to one program."));
            }
            cursor = page.NextCursor;
        } while (cursor is not null);

        return Result.Failure(new RequestError(RequestErrorKind.NotFound,
            "The access grant member was not found in this organization."));
    }

    static Result Invalid(string message) =>
        Result.Failure(new RequestError(RequestErrorKind.Validation, message));
}
