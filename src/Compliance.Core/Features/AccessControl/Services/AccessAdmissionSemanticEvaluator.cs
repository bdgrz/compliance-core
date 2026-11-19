using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>
/// Evaluates one fixed authorization path only after a qualified two-pass source
/// capture. This is not an authentication implementation or a source adapter.
/// </summary>
sealed class AccessAdmissionSemanticEvaluator(AccessAdmissionStableRead reads,
    string supportedPolicyGeneration, string supportedPolicyContentSha256)
{
    // These inputs are supplied by the trusted authentication and route-resolution
    // boundary, never by a request body. The resolver must enumerate the closed
    // policy-defined veto set and select the canonical exact owner for each slot.
    internal enum CredentialScheme { FullParticipants, BdgrzIdentityOnly }
    internal sealed record Credential(Uuid UserId, Uuid IdentityId, Uuid SessionId,
        string Subject, string Issuer, DateTimeOffset NotBefore, DateTimeOffset ExpiresAt,
        CredentialScheme Scheme = CredentialScheme.FullParticipants, bool ExactIdentityBindingCertified = false);
    // Null applicability is unknown, never an assertion that a veto is absent.
    internal sealed record Policy(string Generation, string ContentSha256, bool FixedVetoSetComplete,
        bool? SessionRequired = null, bool? AccountRequired = null, bool? GlobalVetoRequired = null,
        bool AllowBdgrzIdentityOnly = false, bool AllowDevelopmentIdentities = false);
    internal sealed record Session(Uuid UserId, Uuid IdentityId, Uuid SessionId, bool Active);
    internal sealed record ActorVeto(Uuid UserId, bool Denied);
    internal sealed record OperatorGrant(Uuid UserId, bool Active);
    internal sealed record Candidate(Credential Credential, Uuid OperationId, string Target, string IntentSha256,
        IReadOnlyList<AccessAdmissionStableRead.Participant> Participants);
    internal sealed record ProgramCandidate(Candidate Candidate, Uuid TenantId, Uuid TeamId, Uuid RoleId, bool Primary,
        Uuid ProgramId = default, AccessGrantScope? Scope = null);
    internal enum Status { Allowed, Denied, Unavailable }
    internal sealed record Decision(Status Status, AccessAdmissionEvidence? Evidence = null);

    static readonly AccessAdmissionStableRead.Slot[] Common =
    [AccessAdmissionStableRead.Slot.Identity, AccessAdmissionStableRead.Slot.Policy];
    static readonly AccessAdmissionStableRead.Slot[] Optional =
    [AccessAdmissionStableRead.Slot.Session, AccessAdmissionStableRead.Slot.Account,
     AccessAdmissionStableRead.Slot.GlobalVeto];

    internal async ValueTask<Decision> EvaluateOperatorAsync(Candidate candidate, CancellationToken ct = default)
    {
        AccessAdmissionStableRead.Slot[] required = [.. Common, AccessAdmissionStableRead.Slot.Operator];
        if (!HasPossiblePath(candidate.Participants, required))
            return new(Status.Unavailable);
        var capture = await reads.CaptureAsync(candidate.Participants, ct).ConfigureAwait(false);
        if (capture is null)
            return new(Status.Unavailable);
        if (!SupportedPolicy(candidate, capture, required) || !KnownCommonStates(capture) ||
            capture.Observations[AccessAdmissionStableRead.Slot.Operator].State is not OperatorGrant)
            return new(Status.Unavailable);
        if (!CommonEligible(candidate, capture) ||
            !TryState(capture, AccessAdmissionStableRead.Slot.Operator, out OperatorGrant? grant) ||
            grant.UserId != candidate.Credential.UserId || !grant.Active)
            return new(Status.Denied);
        var evidence = Evidence(candidate, capture) with { Operator = Version(capture, AccessAdmissionStableRead.Slot.Operator) };
        return AccessAdmissionEvidenceBinding.BindsCriteriaOperator(evidence, candidate.Credential.UserId,
            new ActorAttribution(candidate.Credential.Subject, candidate.Credential.Issuer),
            candidate.OperationId, candidate.Target, candidate.IntentSha256)
            ? new(Status.Allowed, evidence) : new(Status.Unavailable);
    }

    internal ValueTask<Decision> EvaluateProgramCreationAsync(ProgramCandidate request, CancellationToken ct = default) =>
        EvaluateProgramAsync(request, false, ct);

    internal ValueTask<Decision> EvaluateProgramSelectionAsync(ProgramCandidate request, CancellationToken ct = default) =>
        EvaluateProgramAsync(request, true, ct);

    async ValueTask<Decision> EvaluateProgramAsync(ProgramCandidate request, bool selection, CancellationToken ct)
    {
        var candidate = request.Candidate;
        if (selection && request.ProgramId == Uuid.Empty)
            return new(Status.Unavailable);
        var scope = request.Scope ?? new AccessGrantScope(AccessGrantScopeKind.Organization, request.TenantId);
        if (!request.Primary && (selection && request.Scope is null ||
            scope.ResourceType is not null ||
            !(scope.Kind == AccessGrantScopeKind.Organization && scope.Id == request.TenantId ||
              selection && scope.Kind == AccessGrantScopeKind.Program && scope.Id == request.ProgramId)))
            return new(Status.Unavailable);
        AccessAdmissionStableRead.Slot[] required = request.Primary
            ? [.. Common, AccessAdmissionStableRead.Slot.Tenant, AccessAdmissionStableRead.Slot.Member, AccessAdmissionStableRead.Slot.Primary]
            : [.. Common, AccessAdmissionStableRead.Slot.Tenant, AccessAdmissionStableRead.Slot.Member,
               AccessAdmissionStableRead.Slot.TeamMember, AccessAdmissionStableRead.Slot.Team,
               AccessAdmissionStableRead.Slot.TeamRole, AccessAdmissionStableRead.Slot.Role,
               AccessAdmissionStableRead.Slot.RolePermission];
        if (!HasPossiblePath(candidate.Participants, required))
            return new(Status.Unavailable);
        var capture = await reads.CaptureAsync(candidate.Participants, ct).ConfigureAwait(false);
        if (capture is null)
            return new(Status.Unavailable);
        if (!SupportedPolicy(candidate, capture, required) || !KnownCommonStates(capture) ||
            capture.Observations[AccessAdmissionStableRead.Slot.Tenant].State is not Tenant ||
            capture.Observations[AccessAdmissionStableRead.Slot.Member].State is not Member ||
            (request.Primary ? capture.Observations[AccessAdmissionStableRead.Slot.Primary].State is not PrimaryOrgAdmins :
                capture.Observations[AccessAdmissionStableRead.Slot.TeamMember].State is not TeamMember ||
                capture.Observations[AccessAdmissionStableRead.Slot.Team].State is not Team ||
                capture.Observations[AccessAdmissionStableRead.Slot.TeamRole].State is not TeamRole ||
                capture.Observations[AccessAdmissionStableRead.Slot.Role].State is not Role ||
                capture.Observations[AccessAdmissionStableRead.Slot.RolePermission].State is not RolePermission))
            return new(Status.Unavailable);
        var user = candidate.Credential.UserId;
        var tenantId = request.TenantId;
        if (!CommonEligible(candidate, capture) || tenantId == Uuid.Empty ||
            !TryState(capture, AccessAdmissionStableRead.Slot.Tenant, out Tenant? tenant) || !tenant.IsActive ||
            !Matches(capture, AccessAdmissionStableRead.Slot.Tenant, new Tenant(tenantId).Stream) || tenant.Stream != new Tenant(tenantId).Stream ||
            !TryState(capture, AccessAdmissionStableRead.Slot.Member, out Member? member) ||
            !Matches(capture, AccessAdmissionStableRead.Slot.Member, new Member(tenantId, user).Stream) || member.Stream != new Member(tenantId, user).Stream ||
            member.UserId != user || member.MembershipEpisodeId == Uuid.Empty ||
            member.Affiliation != "client_personnel" ||
            !member.IsAccessEligibleOn(DateOnly.FromDateTime(capture.AdmittedAt.UtcDateTime)))
            return new(Status.Denied);
        var evidence = Evidence(candidate, capture) with
        {
            Tenant = Version(capture, AccessAdmissionStableRead.Slot.Tenant),
            Member = Version(capture, AccessAdmissionStableRead.Slot.Member)
        };
        if (request.Primary)
        {
            if (!TryState(capture, AccessAdmissionStableRead.Slot.Primary, out PrimaryOrgAdmins? primary) ||
                !Matches(capture, AccessAdmissionStableRead.Slot.Primary, new PrimaryOrgAdmins(tenantId).Stream) || primary.Stream != new PrimaryOrgAdmins(tenantId).Stream ||
                !member.IsPrimary || !primary.HasDesignated(user, member.MembershipEpisodeId, member.PrimaryDesignationId))
                return new(Status.Denied);
            return ProgramDecision(candidate, evidence with { Primary = Version(capture, AccessAdmissionStableRead.Slot.Primary) });
        }

        var teamId = request.TeamId;
        var roleId = request.RoleId;
        var memberId = RbacIds.Member(tenantId, user);
        if (teamId == Uuid.Empty || roleId == Uuid.Empty || roleId == BuiltInRbac.AdvisorRoleId(tenantId) ||
            roleId == BuiltInRbac.AttestRoleId(tenantId) ||
            !TryState(capture, AccessAdmissionStableRead.Slot.TeamMember, out TeamMember? teamMember) ||
            !Matches(capture, AccessAdmissionStableRead.Slot.TeamMember, new TeamMember(tenantId, teamId, memberId).Stream) || teamMember.Stream != new TeamMember(tenantId, teamId, memberId).Stream ||
            !teamMember.IsAssigned || teamMember.MembershipEpisodeId != member.MembershipEpisodeId ||
            !TryState(capture, AccessAdmissionStableRead.Slot.Team, out Team? team) || !team.IsActive ||
            !Matches(capture, AccessAdmissionStableRead.Slot.Team, new Team(tenantId, teamId).Stream) || team.Stream != new Team(tenantId, teamId).Stream ||
            !TryState(capture, AccessAdmissionStableRead.Slot.TeamRole, out TeamRole? teamRole) || !teamRole.IsAssigned ||
            !Matches(capture, AccessAdmissionStableRead.Slot.TeamRole, new TeamRole(tenantId, teamId, roleId, scope: scope).Stream) || teamRole.Stream != new TeamRole(tenantId, teamId, roleId, scope: scope).Stream ||
            teamRole.TenantId != tenantId || teamRole.TeamId != teamId || teamRole.RoleId != roleId ||
            teamRole.Scope != scope ||
            !TryState(capture, AccessAdmissionStableRead.Slot.Role, out Role? role) || !role.IsActive ||
            !Matches(capture, AccessAdmissionStableRead.Slot.Role, new Role(tenantId, roleId).Stream) || role.Stream != new Role(tenantId, roleId).Stream ||
            !TryState(capture, AccessAdmissionStableRead.Slot.RolePermission, out RolePermission? permission) || !permission.IsAssigned ||
            !Matches(capture, AccessAdmissionStableRead.Slot.RolePermission, new RolePermission(tenantId, roleId, RbacPermissions.ProgramManage).Stream) || permission.Stream != new RolePermission(tenantId, roleId, RbacPermissions.ProgramManage).Stream ||
            permission.TenantId != tenantId || permission.RoleId != roleId || permission.Permission != RbacPermissions.ProgramManage)
            return new(Status.Denied);
        return ProgramDecision(candidate, evidence with
        {
            TeamMember = Version(capture, AccessAdmissionStableRead.Slot.TeamMember),
            Team = Version(capture, AccessAdmissionStableRead.Slot.Team),
            TeamRole = Version(capture, AccessAdmissionStableRead.Slot.TeamRole),
            Role = Version(capture, AccessAdmissionStableRead.Slot.Role),
            RolePermission = Version(capture, AccessAdmissionStableRead.Slot.RolePermission)
        });
    }

    static bool CommonEligible(Candidate candidate, AccessAdmissionStableRead.Capture capture)
    {
        var credential = candidate.Credential;
        var instant = capture.AdmittedAt;
        return credential.UserId != Uuid.Empty && credential.IdentityId != Uuid.Empty &&
            !string.IsNullOrWhiteSpace(credential.Subject) && !string.IsNullOrWhiteSpace(credential.Issuer) &&
            credential.NotBefore <= instant && instant < credential.ExpiresAt &&
            candidate.OperationId != Uuid.Empty && !string.IsNullOrWhiteSpace(candidate.Target) &&
            candidate.IntentSha256 is { Length: 64 } && candidate.IntentSha256.All(static c => c is >= '0' and <= '9' or >= 'a' and <= 'f') &&
            TryState(capture, AccessAdmissionStableRead.Slot.Identity, out UserIdentity? identity) &&
            Matches(capture, AccessAdmissionStableRead.Slot.Identity, new UserIdentity(credential.IdentityId).Stream) && identity.Stream == new UserIdentity(credential.IdentityId).Stream &&
            identity.IsRegistered && !identity.IsRevoked && identity.UserId == credential.UserId &&
            TryState(capture, AccessAdmissionStableRead.Slot.Policy, out Policy? policy) &&
            (policy.AllowDevelopmentIdentities || identity.Provider != DeveloperUserRegistration.Provider) &&
            (!capture.Observations.ContainsKey(AccessAdmissionStableRead.Slot.Session) ||
             TryState(capture, AccessAdmissionStableRead.Slot.Session, out Session? session) && session.Active &&
             credential.SessionId != Uuid.Empty && session.UserId == credential.UserId && session.IdentityId == credential.IdentityId && session.SessionId == credential.SessionId) &&
            (!capture.Observations.ContainsKey(AccessAdmissionStableRead.Slot.Account) ||
             TryState(capture, AccessAdmissionStableRead.Slot.Account, out ActorVeto? account) && account.UserId == credential.UserId && !account.Denied) &&
            (!capture.Observations.ContainsKey(AccessAdmissionStableRead.Slot.GlobalVeto) ||
             TryState(capture, AccessAdmissionStableRead.Slot.GlobalVeto, out ActorVeto? veto) && veto.UserId == credential.UserId && !veto.Denied);
    }

    bool SupportedPolicy(Candidate candidate, AccessAdmissionStableRead.Capture capture, AccessAdmissionStableRead.Slot[] required) =>
        TryState(capture, AccessAdmissionStableRead.Slot.Policy, out Policy? policy) && policy.FixedVetoSetComplete &&
        policy.SessionRequired.HasValue && policy.AccountRequired.HasValue && policy.GlobalVetoRequired.HasValue &&
        (!policy.SessionRequired.Value || candidate.Credential.SessionId != Uuid.Empty) &&
        (candidate.Credential.Scheme == CredentialScheme.FullParticipants
            ? policy.SessionRequired.Value && policy.AccountRequired.Value && policy.GlobalVetoRequired.Value
            : candidate.Credential.Scheme == CredentialScheme.BdgrzIdentityOnly && policy.AllowBdgrzIdentityOnly &&
              !policy.SessionRequired.Value && !policy.AccountRequired.Value && !policy.GlobalVetoRequired.Value &&
              candidate.Credential.ExactIdentityBindingCertified && candidate.Credential.Issuer == "bdgrz") &&
        HasExactly(candidate.Participants, [.. required,
            .. (policy.SessionRequired.Value ? new[] { AccessAdmissionStableRead.Slot.Session } : []),
            .. (policy.AccountRequired.Value ? new[] { AccessAdmissionStableRead.Slot.Account } : []),
            .. (policy.GlobalVetoRequired.Value ? new[] { AccessAdmissionStableRead.Slot.GlobalVeto } : [])]) &&
        !string.IsNullOrWhiteSpace(supportedPolicyGeneration) && supportedPolicyContentSha256 is { Length: 64 } &&
        supportedPolicyContentSha256.All(static c => c is >= '0' and <= '9' or >= 'a' and <= 'f') &&
        policy.Generation == supportedPolicyGeneration && policy.ContentSha256 == supportedPolicyContentSha256;

    static bool KnownCommonStates(AccessAdmissionStableRead.Capture capture) =>
        capture.Observations[AccessAdmissionStableRead.Slot.Identity].State is UserIdentity &&
        (!capture.Observations.ContainsKey(AccessAdmissionStableRead.Slot.Session) || capture.Observations[AccessAdmissionStableRead.Slot.Session].State is Session) &&
        (!capture.Observations.ContainsKey(AccessAdmissionStableRead.Slot.Account) || capture.Observations[AccessAdmissionStableRead.Slot.Account].State is ActorVeto) &&
        (!capture.Observations.ContainsKey(AccessAdmissionStableRead.Slot.GlobalVeto) || capture.Observations[AccessAdmissionStableRead.Slot.GlobalVeto].State is ActorVeto);

    static Decision ProgramDecision(Candidate candidate, AccessAdmissionEvidence evidence) =>
        AccessAdmissionEvidenceBinding.BindsProgramCreation(evidence, candidate.Credential.UserId,
            new ActorAttribution(candidate.Credential.Subject, candidate.Credential.Issuer),
            candidate.OperationId, candidate.Target, candidate.IntentSha256)
            ? new(Status.Allowed, evidence) : new(Status.Unavailable);

    static bool HasExactly(IReadOnlyList<AccessAdmissionStableRead.Participant> participants, AccessAdmissionStableRead.Slot[] roles) =>
        participants is not null && participants.Count == roles.Length &&
        roles.All(role => participants.Count(participant => participant is not null && participant.Role == role) == 1);

    static bool HasPossiblePath(IReadOnlyList<AccessAdmissionStableRead.Participant> participants, AccessAdmissionStableRead.Slot[] required) =>
        participants is not null && participants.Count >= required.Length && participants.Count <= required.Length + Optional.Length &&
        required.All(role => participants.Count(participant => participant is not null && participant.Role == role) == 1) &&
        participants.All(participant => participant is not null && (required.Contains(participant.Role) || Optional.Contains(participant.Role))) &&
        Optional.All(role => participants.Count(participant => participant is not null && participant.Role == role) <= 1);

    static bool TryState<T>(AccessAdmissionStableRead.Capture capture, AccessAdmissionStableRead.Slot slot,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out T? state) where T : class
    {
        state = capture.Observations.TryGetValue(slot, out var observation) ? observation.State as T : null;
        return state is not null;
    }

    static bool Matches(AccessAdmissionStableRead.Capture capture, AccessAdmissionStableRead.Slot slot, EventStreamAddress stream) =>
        string.Equals(Version(capture, slot).Stream, stream.ToString(), StringComparison.Ordinal);

    static AccessAdmissionSourceVersion Version(AccessAdmissionStableRead.Capture capture, AccessAdmissionStableRead.Slot slot) =>
        capture.Observations[slot].Version;

    static AccessAdmissionSourceVersion? OptionalVersion(AccessAdmissionStableRead.Capture capture, AccessAdmissionStableRead.Slot slot) =>
        capture.Observations.TryGetValue(slot, out var observation) ? observation.Version : null;

    static AccessAdmissionEvidence Evidence(Candidate candidate, AccessAdmissionStableRead.Capture capture) =>
        new(candidate.Credential.UserId, candidate.Credential.Subject, candidate.Credential.Issuer,
            candidate.OperationId, candidate.Target, candidate.IntentSha256, capture.AdmittedAt,
            ((Policy)capture.Observations[AccessAdmissionStableRead.Slot.Policy].State).Generation,
            Version(capture, AccessAdmissionStableRead.Slot.Identity), Version(capture, AccessAdmissionStableRead.Slot.Policy))
        {
            FormatVersion = 1,
            PolicyContentSha256 = ((Policy)capture.Observations[AccessAdmissionStableRead.Slot.Policy].State).ContentSha256,
            Session = OptionalVersion(capture, AccessAdmissionStableRead.Slot.Session),
            Account = OptionalVersion(capture, AccessAdmissionStableRead.Slot.Account),
            GlobalVeto = OptionalVersion(capture, AccessAdmissionStableRead.Slot.GlobalVeto)
        };
}
