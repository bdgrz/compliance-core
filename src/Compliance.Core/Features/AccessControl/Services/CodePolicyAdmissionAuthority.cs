using System.Text;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>Admits criteria operators and program managers from actual owner reads.</summary>
/// <remarks>
/// This code policy records real observed owner positions, not a fictitious policy stream.
/// Rechecking the owners narrows concurrent changes but is not an atomic cross-owner transaction.
/// The existing Portia request authorizers remain in force. No registration is performed here.
/// </remarks>
sealed class CodePolicyAdmissionAuthority(ITrustedAdmissionCredentialResolver credentials,
    IAggregateReader aggregates, IDomainEventReader events, TimeProvider clock,
    DeveloperUserRegistration developer)
    : IAccessAdmissionAuthority
{
    internal const string PolicyGeneration = "bdgrz-criteria-program-code-policy-v1";
    internal const string DeveloperPolicyGeneration = "bdgrz-criteria-program-code-policy-v1-development";
    // Keep this canonical rule descriptor in sync with the decisions below. It is a code policy,
    // not a claim that a policy document or policy stream was observed.
    internal const string PolicyRules = "v1|credential:bdgrz-session,identity-current|program:tenant-active,client-personnel-current,confirmed-primary-or-current-team-role,program-manage|create-scope:organization|selection-scope:organization-or-exact-program|exclude-advisor-attest";
    internal static string PolicyDigest => ContentDigest.Sha256(Encoding.UTF8.GetBytes(PolicyRules + "|development:disabled|criteria:per-user-operator-current"));
    internal static string DeveloperPolicyDigest => ContentDigest.Sha256(Encoding.UTF8.GetBytes(PolicyRules + "|development:enabled|criteria:operator-bypass"));

    public ValueTask<AccessAdmissionEvidence?> AdmitCriteriaOperatorAsync(IRequestContext context,
        Uuid operationId, string target, string intentSha256, CancellationToken ct = default) =>
        GuardAsync(() => CriteriaAsync(context, operationId, target, intentSha256, ct), ct);

    async ValueTask<AccessAdmissionEvidence?> CriteriaAsync(IRequestContext context,
        Uuid operationId, string target, string intentSha256, CancellationToken ct)
    {
        var credential = await CredentialAsync(context, operationId, target, intentSha256, ct).ConfigureAwait(false);
        var identity = await ObserveAsync(new UserIdentity(credential.IdentityId), ct).ConfigureAwait(false);
        if (identity is null)
            throw new AccessAdmissionUnavailableException();
        var identityValid = ValidIdentity(identity.Value.Owner, credential);
        if (developer.Enabled)
        {
            if (!await UnchangedAsync(identity.Value, new UserIdentity(credential.IdentityId), ct).ConfigureAwait(false) || !credentials.IsQualified)
                throw new AccessAdmissionUnavailableException();
            return identityValid ? Evidence(credential, operationId, target, intentSha256, identity.Value.Version) : null;
        }
        var operatorOwner = await ObserveAsync(new PlatformOperator(credential.UserId), ct).ConfigureAwait(false);
        if (operatorOwner is null)
            throw new AccessAdmissionUnavailableException();
        var allowed = operatorOwner.Value.Owner.IsActive;
        if (!await UnchangedAsync(identity.Value, new UserIdentity(credential.IdentityId), ct).ConfigureAwait(false) ||
            !await UnchangedAsync(operatorOwner.Value, new PlatformOperator(credential.UserId), ct).ConfigureAwait(false) || !credentials.IsQualified)
            throw new AccessAdmissionUnavailableException();
        if (!identityValid || !allowed)
            return null;
        return Evidence(credential, operationId, target, intentSha256, identity.Value.Version) with { Operator = operatorOwner.Value.Version };
    }

    public ValueTask<AccessAdmissionEvidence?> AdmitProgramCreationAsync(IRequestContext context,
        Uuid tenantId, Uuid programId, string target, string intentSha256, CancellationToken ct = default) =>
        GuardAsync(() => ProgramAsync(context, tenantId, programId, programId, target, intentSha256, false, ct), ct);

    public ValueTask<AccessAdmissionEvidence?> AdmitProgramSelectionAsync(IRequestContext context,
        Uuid tenantId, Uuid programId, Uuid operationId, string target, string intentSha256, CancellationToken ct = default) =>
        GuardAsync(() => ProgramAsync(context, tenantId, programId, operationId, target, intentSha256, true, ct), ct);

    async ValueTask<AccessAdmissionEvidence?> ProgramAsync(IRequestContext context, Uuid tenantId, Uuid programId,
        Uuid operationId, string target, string digest, bool selection, CancellationToken ct)
    {
        if (tenantId == Uuid.Empty || programId == Uuid.Empty || target != new ComplianceProgram(tenantId, programId).Stream.ToString())
            throw new AccessAdmissionUnavailableException();
        var credential = await CredentialAsync(context, operationId, target, digest, ct).ConfigureAwait(false);
        var identity = await ObserveAsync(new UserIdentity(credential.IdentityId), ct).ConfigureAwait(false);
        var tenant = await ObserveAsync(new Tenant(tenantId), ct).ConfigureAwait(false);
        var member = await ObserveAsync(new Member(tenantId, credential.UserId), ct).ConfigureAwait(false);
        if (identity is null || tenant is null || member is null)
            throw new AccessAdmissionUnavailableException();
        var current = member.Value.Owner;
        if (!ValidIdentity(identity.Value.Owner, credential) || !tenant.Value.Owner.IsActive ||
            current.UserId != credential.UserId || current.Affiliation != "client_personnel" ||
            !current.IsAccessEligibleOn(DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)))
            return null;
        if (!current.IsPrimary)
            return await TeamPathAsync(credential, tenantId, programId, operationId, target, digest, selection,
                identity.Value, tenant.Value, member.Value, ct).ConfigureAwait(false);
        var primary = await ObserveAsync(new PrimaryOrgAdmins(tenantId), ct).ConfigureAwait(false);
        if (primary is null)
            return await TeamPathAsync(credential, tenantId, programId, operationId, target, digest, selection,
            identity.Value, tenant.Value, member.Value, ct).ConfigureAwait(false);
        var allowed = primary.Value.Owner.HasDesignated(credential.UserId, current.MembershipEpisodeId, current.PrimaryDesignationId) &&
            BuiltInRolePermissions.Create(tenantId).Any(grant =>
                grant.RoleId == BuiltInRbac.TenantAdministrationRoleId(tenantId) && grant.Permission == RbacPermissions.ProgramManage);
        if (!await UnchangedAsync(identity.Value, new UserIdentity(credential.IdentityId), ct).ConfigureAwait(false) ||
            !await UnchangedAsync(tenant.Value, new Tenant(tenantId), ct).ConfigureAwait(false) ||
            !await UnchangedAsync(member.Value, new Member(tenantId, credential.UserId), ct).ConfigureAwait(false) ||
            !await UnchangedAsync(primary.Value, new PrimaryOrgAdmins(tenantId), ct).ConfigureAwait(false) || !credentials.IsQualified)
            throw new AccessAdmissionUnavailableException();
        if (!allowed)
            return await TeamPathAsync(credential, tenantId, programId, operationId, target, digest, selection,
            identity.Value, tenant.Value, member.Value, ct).ConfigureAwait(false);
        return Evidence(credential, operationId, target, digest, identity.Value.Version) with
        { Tenant = tenant.Value.Version, Member = member.Value.Version, Primary = primary.Value.Version };
    }

    async ValueTask<AccessAdmissionEvidence?> TeamPathAsync(AccessAdmissionSemanticEvaluator.Credential credential,
        Uuid tenantId, Uuid programId, Uuid operationId, string target, string digest, bool selection,
        (UserIdentity Owner, AccessAdmissionSourceVersion Version) identity,
        (Tenant Owner, AccessAdmissionSourceVersion Version) tenant,
        (Member Owner, AccessAdmissionSourceVersion Version) member, CancellationToken ct)
    {
        var memberId = RbacIds.Member(tenantId, credential.UserId);
        var pattern = EventStreamPattern.ForPattern(tenantId.ToString(), "rbac-team-roles");
        await foreach (var record in events.ReadAsync(pattern, EventCursor.Start, ct).ConfigureAwait(false))
        {
            if (record.Event is not TeamRoleAssigned assignment || assignment.TenantId != tenantId ||
                assignment.RoleId == BuiltInRbac.AdvisorRoleId(tenantId) || assignment.RoleId == BuiltInRbac.AttestRoleId(tenantId))
                continue;
            var scope = TeamRoleScopes.Normalize(tenantId, assignment.Scope);
            if (!TeamRoleScopes.IsValid(tenantId, scope) || scope.ResourceType is not null ||
                !(scope.Kind == AccessGrantScopeKind.Organization && scope.Id == tenantId ||
                  selection && scope.Kind == AccessGrantScopeKind.Program && scope.Id == programId))
                continue;
            var teamRoleOwner = new TeamRole(tenantId, assignment.TeamId, assignment.RoleId, scope: scope);
            if (record.Stream != teamRoleOwner.Stream || record.Event.Metadata.AggregateId != teamRoleOwner.Id)
                continue;
            var teamRole = await ObserveAsync(teamRoleOwner, ct).ConfigureAwait(false);
            if (teamRole is null || !teamRole.Value.Owner.IsAssigned || teamRole.Value.Owner.AssignmentId != assignment.CommandId)
                continue;
            var teamMember = await ObserveAsync(new TeamMember(tenantId, assignment.TeamId, memberId), ct).ConfigureAwait(false);
            var team = await ObserveAsync(new Team(tenantId, assignment.TeamId), ct).ConfigureAwait(false);
            var role = await ObserveAsync(new Role(tenantId, assignment.RoleId), ct).ConfigureAwait(false);
            var permission = await ObserveAsync(new RolePermission(tenantId, assignment.RoleId, RbacPermissions.ProgramManage), ct).ConfigureAwait(false);
            if (teamMember is null || team is null || role is null || permission is null ||
                !teamMember.Value.Owner.IsAssigned || teamMember.Value.Owner.MembershipEpisodeId != member.Owner.MembershipEpisodeId ||
                !team.Value.Owner.IsActive || !role.Value.Owner.IsActive || !permission.Value.Owner.IsAssigned)
                continue;
            if (!await UnchangedAsync(identity, new UserIdentity(credential.IdentityId), ct).ConfigureAwait(false) ||
                !await UnchangedAsync(tenant, new Tenant(tenantId), ct).ConfigureAwait(false) ||
                !await UnchangedAsync(member, new Member(tenantId, credential.UserId), ct).ConfigureAwait(false) ||
                !await UnchangedAsync(teamMember.Value, new TeamMember(tenantId, assignment.TeamId, memberId), ct).ConfigureAwait(false) ||
                !await UnchangedAsync(team.Value, new Team(tenantId, assignment.TeamId), ct).ConfigureAwait(false) ||
                !await UnchangedAsync(teamRole.Value, new TeamRole(tenantId, assignment.TeamId, assignment.RoleId, scope: scope), ct).ConfigureAwait(false) ||
                !await UnchangedAsync(role.Value, new Role(tenantId, assignment.RoleId), ct).ConfigureAwait(false) ||
                !await UnchangedAsync(permission.Value, new RolePermission(tenantId, assignment.RoleId, RbacPermissions.ProgramManage), ct).ConfigureAwait(false) ||
                !credentials.IsQualified)
                throw new AccessAdmissionUnavailableException();
            return Evidence(credential, operationId, target, digest, identity.Version) with
            {
                Tenant = tenant.Version,
                Member = member.Version,
                TeamMember = teamMember.Value.Version,
                Team = team.Value.Version,
                TeamRole = teamRole.Value.Version,
                Role = role.Value.Version,
                RolePermission = permission.Value.Version,
            };
        }
        // No matching observed path does not prove no other grant exists.
        throw new AccessAdmissionUnavailableException();
    }

    async ValueTask<AccessAdmissionSemanticEvaluator.Credential> CredentialAsync(IRequestContext context,
        Uuid operationId, string target, string digest, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (RequestActor.IsSystem(context.Actor) || !credentials.IsQualified || operationId == Uuid.Empty ||
            digest is not { Length: 64 } || !digest.All(static c => c is >= '0' and <= '9' or >= 'a' and <= 'f'))
            throw new AccessAdmissionUnavailableException();
        try
        { if (EventStreamAddress.Parse(target).ToString() != target) throw new AccessAdmissionUnavailableException(); }
        catch (ArgumentException) { throw new AccessAdmissionUnavailableException(); }
        catch (FormatException) { throw new AccessAdmissionUnavailableException(); }
        var credential = await credentials.ResolveAsync(context, ct).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        if (credential is null || !credentials.IsQualified || credential.UserId == Uuid.Empty || credential.IdentityId == Uuid.Empty ||
            credential.Subject != credential.UserId.ToString() || credential.Issuer != "bdgrz" ||
            credential.NotBefore > now || now >= credential.ExpiresAt ||
            credential.Scheme != AccessAdmissionSemanticEvaluator.CredentialScheme.BdgrzIdentityOnly || !credential.ExactIdentityBindingCertified)
            throw new AccessAdmissionUnavailableException();
        return credential;
    }

    bool ValidIdentity(UserIdentity identity, AccessAdmissionSemanticEvaluator.Credential credential) =>
        identity.IsRegistered && !identity.IsRevoked && identity.UserId == credential.UserId &&
        identity.Provider is not null && (developer.Enabled || identity.Provider != DeveloperUserRegistration.Provider);

    AccessAdmissionEvidence Evidence(AccessAdmissionSemanticEvaluator.Credential credential, Uuid operationId,
        string target, string digest, AccessAdmissionSourceVersion identity) =>
        new(credential.UserId, credential.Subject, credential.Issuer, operationId, target, digest,
            clock.GetUtcNow().ToUniversalTime(), developer.Enabled ? DeveloperPolicyGeneration : PolicyGeneration, identity, null)
        { FormatVersion = 2, PolicyContentSha256 = developer.Enabled ? DeveloperPolicyDigest : PolicyDigest };

    async ValueTask<(T Owner, AccessAdmissionSourceVersion Version)?> ObserveAsync<T>(T owner, CancellationToken ct)
        where T : Aggregate
    {
        var loaded = await aggregates.HydrateAsync(owner, ct).ConfigureAwait(false);
        if (loaded.CommittedStreamPosition == 0)
            return null;
        await using var source = events.ReadAsync(loaded.Stream, 0, ct).GetAsyncEnumerator(ct);
        if (!await source.MoveNextAsync().ConfigureAwait(false))
            return null;
        var first = source.Current;
        if (first.Stream != loaded.Stream || first.ResourceOffset != 0 || first.Event.Metadata.AggregateId != loaded.Id ||
            first.Event.Metadata.EventId == Uuid.Empty)
            return null;
        return (loaded, new AccessAdmissionSourceVersion(loaded.Stream.ToString(),
            "first-event:" + first.Event.Metadata.EventId, loaded.CommittedStreamPosition));
    }

    async ValueTask<bool> UnchangedAsync<T>((T Owner, AccessAdmissionSourceVersion Version) first, T owner,
        CancellationToken ct) where T : Aggregate =>
        (await ObserveAsync(owner, ct).ConfigureAwait(false)) is { } current && current.Version == first.Version;

    static async ValueTask<AccessAdmissionEvidence?> GuardAsync(Func<ValueTask<AccessAdmissionEvidence?>> action,
        CancellationToken ct)
    {
        try
        { return await action().ConfigureAwait(false); }
        catch (FitzException) { ct.ThrowIfCancellationRequested(); throw new AccessAdmissionUnavailableException(); }
        catch (ImmutableContentIntegrityException) { ct.ThrowIfCancellationRequested(); throw new AccessAdmissionUnavailableException(); }
        catch (IOException) { ct.ThrowIfCancellationRequested(); throw new AccessAdmissionUnavailableException(); }
        catch (TimeoutException) { ct.ThrowIfCancellationRequested(); throw new AccessAdmissionUnavailableException(); }
    }
}
