using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class ProgramCreationAdmissionAuthorityTests
{
    static readonly DateTimeOffset At = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
    const string Digest = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task ShouldReturnBoundEvidenceGivenAQualifiedCompletePrimaryPath()
    {
        // Arrange
        var fixture = new Fixture();

        // Act
        var result = await fixture.AdmitAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(fixture.UserId, result.UserId);
        Assert.Equal(fixture.ProgramId, result.OperationId);
        Assert.Equal(fixture.Target, result.Target);
        Assert.Equal(Digest, result.IntentSha256);
        Assert.NotNull(result.Tenant);
        Assert.NotNull(result.Member);
        Assert.NotNull(result.Primary);
        Assert.Equal(16, fixture.Source.Reads);
        Assert.True(fixture.Routes.Checked);
    }

    [Fact]
    public async Task ShouldDenyGivenAnInactiveTenantAndQualifiedCompleteAlternatives()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.Source.Set(AccessAdmissionStableRead.Slot.Tenant, new Tenant(fixture.TenantId));

        // Act
        var result = await fixture.AdmitAsync();

        // Assert
        Assert.Null(result);
        Assert.True(fixture.Routes.Checked);
    }

    [Fact]
    public async Task ShouldReturnBoundEvidenceGivenAQualifiedOrganizationScopedTeamGrant()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.UseTeamPath();

        // Act
        var result = await fixture.AdmitAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.TeamMember);
        Assert.NotNull(result.Team);
        Assert.NotNull(result.TeamRole);
        Assert.NotNull(result.Role);
        Assert.NotNull(result.RolePermission);
        Assert.Null(result.Primary);
        Assert.Equal(24, fixture.Source.Reads);
    }

    [Fact]
    public async Task ShouldDenyGivenOnlyAProgramScopedTeamGrant()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.UseTeamPath(organizationScope: false);

        // Act
        var result = await fixture.AdmitAsync();

        // Assert
        Assert.Null(result);
        Assert.True(fixture.Routes.Checked);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("unqualified")]
    [InlineData("alternatives")]
    [InlineData("changed")]
    [InlineData("tenant")]
    [InlineData("user")]
    public async Task ShouldRemainUnavailableGivenUnqualifiedRouting(string failure)
    {
        // Arrange
        var fixture = new Fixture();
        if (failure == "missing")
            fixture.Routes.Missing = true;
        if (failure == "unqualified")
            fixture.Routes.Qualified = false;
        if (failure == "alternatives")
            fixture.Routes.Path = fixture.Routes.Path with { AlternativesResolved = false };
        if (failure == "changed")
            fixture.Routes.Current = false;
        if (failure == "tenant")
            fixture.Routes.Path = fixture.Routes.Path with { TenantId = Uuid.CreateVersion4() };
        if (failure == "user")
            fixture.Routes.Path = fixture.Routes.Path with { UserId = Uuid.CreateVersion4() };

        // Act
        // Assert
        await Assert.ThrowsAsync<AccessAdmissionUnavailableException>(async () => await fixture.AdmitAsync());
    }

    [Fact]
    public async Task ShouldNotReadSourcesGivenAnOversizedProgramPath()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.Routes.Path = fixture.Routes.Path with
        {
            Participants = fixture.Routes.Path.Participants.Concat([fixture.Routes.Path.Participants[0]]).ToArray(),
        };

        // Act
        // Assert
        await Assert.ThrowsAsync<AccessAdmissionUnavailableException>(async () => await fixture.AdmitAsync());
        Assert.Equal(0, fixture.Source.Reads);
    }

    [Fact]
    public async Task ShouldRejectProgramTargetGivenItIsAnotherTargetBeforeReadingSources()
    {
        // Arrange
        var fixture = new Fixture();
        var otherTarget = new ComplianceProgram(fixture.TenantId, Uuid.CreateVersion4()).Stream.ToString();

        // Act
        // Assert
        await Assert.ThrowsAsync<AccessAdmissionUnavailableException>(async () =>
            await fixture.Authority.AdmitProgramCreationAsync(new RequestDispatchContext(fixture.Actor), fixture.TenantId, fixture.ProgramId, otherTarget, Digest));
        Assert.Equal(0, fixture.Source.Reads);
    }

    [Fact]
    public async Task ShouldRemainUnavailableGivenSourceChangesDuringCapture()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.Source.ChangeOnRead = 9;

        // Act
        // Assert
        await Assert.ThrowsAsync<AccessAdmissionUnavailableException>(async () => await fixture.AdmitAsync());
    }

    [Fact]
    public async Task ShouldNotReportDenialGivenUnresolvedAlternativeGrants()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.Source.Set(AccessAdmissionStableRead.Slot.Tenant, new Tenant(fixture.TenantId));
        fixture.Routes.Path = fixture.Routes.Path with { AlternativesResolved = false };

        // Act
        // Assert
        await Assert.ThrowsAsync<AccessAdmissionUnavailableException>(async () => await fixture.AdmitAsync());
        Assert.Equal(0, fixture.Source.Reads);
    }

    [Fact]
    public async Task ShouldRemainUnavailableGivenRouteQualificationLostDuringCapture()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.Source.OnRead = () => fixture.Routes.Qualified = false;

        // Act
        // Assert
        await Assert.ThrowsAsync<AccessAdmissionUnavailableException>(async () => await fixture.AdmitAsync());
    }

    [Fact]
    public async Task ShouldKeepProgramSelectionUnavailableGivenItsAuthorityIsUnqualified()
    {
        // Arrange
        var fixture = new Fixture();

        // Act
        // Assert
        await Assert.ThrowsAsync<AccessAdmissionUnavailableException>(async () =>
            await fixture.Authority.AdmitProgramSelectionAsync(new RequestDispatchContext(fixture.Actor), fixture.TenantId, fixture.ProgramId,
                Uuid.CreateVersion4(), fixture.Target, Digest));
    }

    [Fact]
    public async Task ShouldAdmitPrimaryAdministratorGivenQualifiedSelectionRoute()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.EnableSelection();
        var operation = Uuid.CreateVersion4();

        // Act
        var evidence = await fixture.AdmitSelectionAsync(operation);

        // Assert
        Assert.NotNull(evidence);
        Assert.Equal(operation, evidence.OperationId);
        Assert.Equal(fixture.Target, evidence.Target);
        Assert.NotNull(evidence.Primary);
        Assert.True(fixture.SelectionRoutes!.Checked);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldAdmitSelectionGivenExactOrganizationOrProgramTeamGrant(bool organizationScope)
    {
        // Arrange
        var fixture = new Fixture();
        fixture.UseTeamPath(organizationScope);
        fixture.EnableSelection(organizationScope);

        // Act
        var evidence = await fixture.AdmitSelectionAsync(Uuid.CreateVersion4());

        // Assert
        Assert.NotNull(evidence);
        Assert.NotNull(evidence.TeamRole);
        Assert.NotNull(evidence.RolePermission);
        Assert.Null(evidence.Primary);
        Assert.Equal(24, fixture.Source.Reads);
    }

    [Theory]
    [InlineData("program")]
    [InlineData("tenant")]
    [InlineData("user")]
    [InlineData("scope")]
    [InlineData("alternatives")]
    [InlineData("changed")]
    [InlineData("unqualified")]
    [InlineData("missing")]
    public async Task ShouldKeepSelectionUnavailableGivenUnqualifiedRouting(string failure)
    {
        // Arrange
        var fixture = new Fixture();
        fixture.UseTeamPath(organizationScope: false);
        fixture.EnableSelection(organizationScope: false);
        var route = fixture.SelectionRoutes!;
        if (failure == "program")
            route.Path = route.Path with { ProgramId = Uuid.CreateVersion4() };
        if (failure == "tenant")
            route.Path = route.Path with { TenantId = Uuid.CreateVersion4() };
        if (failure == "user")
            route.Path = route.Path with { UserId = Uuid.CreateVersion4() };
        if (failure == "scope")
            route.Path = route.Path with { Scope = new(AccessGrantScopeKind.Program, Uuid.CreateVersion4()) };
        if (failure == "alternatives")
            route.Path = route.Path with { AlternativesResolved = false };
        if (failure == "changed")
            route.Current = false;
        if (failure == "unqualified")
            route.Qualified = false;
        if (failure == "missing")
            route.Missing = true;

        // Act
        // Assert
        await Assert.ThrowsAsync<AccessAdmissionUnavailableException>(async () =>
            await fixture.AdmitSelectionAsync(Uuid.CreateVersion4()));
    }

    [Fact]
    public async Task ShouldKeepSelectionUnavailableGivenSourceChangesDuringCapture()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.EnableSelection();
        fixture.Source.ChangeOnRead = 9;

        // Act
        // Assert
        await Assert.ThrowsAsync<AccessAdmissionUnavailableException>(async () =>
            await fixture.AdmitSelectionAsync(Uuid.CreateVersion4()));
    }

    [Fact]
    public async Task ShouldKeepSelectionUnavailableGivenMissingParticipantEvidence()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.UseTeamPath(organizationScope: false);
        fixture.EnableSelection(organizationScope: false);
        fixture.Source.Remove(AccessAdmissionStableRead.Slot.RolePermission);

        // Act
        // Assert
        await Assert.ThrowsAsync<AccessAdmissionUnavailableException>(async () =>
            await fixture.AdmitSelectionAsync(Uuid.CreateVersion4()));
    }

    [Fact]
    public async Task ShouldDenySelectionGivenExcludedAdvisorRole()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.UseTeamPath(organizationScope: false);
        fixture.UseNonPrimaryMemberEpisode();

        // Act
        var decision = await fixture.EvaluateExcludedRoleAsync();

        // Assert
        Assert.Equal(AccessAdmissionSemanticEvaluator.Status.Denied, decision.Status);
        Assert.Null(decision.Evidence);
    }

    [Fact]
    public async Task ShouldKeepSelectionUnavailableGivenOversizedParticipantRoute()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.EnableSelection();
        var route = fixture.SelectionRoutes!;
        route.Path = route.Path with { Participants = route.Path.Participants.Concat([route.Path.Participants[0]]).ToArray() };

        // Act
        // Assert
        await Assert.ThrowsAsync<AccessAdmissionUnavailableException>(async () =>
            await fixture.AdmitSelectionAsync(Uuid.CreateVersion4()));
        Assert.Equal(0, fixture.Source.Reads);
    }

    [Fact]
    public async Task ShouldKeepSelectionUnavailableGivenAnotherProgramTarget()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.EnableSelection();
        var target = new ComplianceProgram(fixture.TenantId, Uuid.CreateVersion4()).Stream.ToString();

        // Act
        // Assert
        await Assert.ThrowsAsync<AccessAdmissionUnavailableException>(async () =>
            await fixture.SelectionAuthority.AdmitProgramSelectionAsync(new RequestDispatchContext(fixture.Actor), fixture.TenantId, fixture.ProgramId,
                Uuid.CreateVersion4(), target, Digest));
        Assert.Equal(0, fixture.Source.Reads);
    }

    [Fact]
    public async Task ShouldAdmitPrimaryProgramPathsGivenCertifiedIdentityOnlyPolicy()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.UseIdentityOnly();
        fixture.EnableSelection();

        // Act
        var creation = await fixture.AdmitAsync();
        var selection = await fixture.AdmitSelectionAsync(Uuid.CreateVersion4());

        // Assert
        Assert.NotNull(creation);
        Assert.NotNull(selection);
        Assert.Null(creation.Session);
        Assert.NotNull(creation.Tenant);
        Assert.NotNull(creation.Member);
        Assert.NotNull(creation.Primary);
    }

    [Fact]
    public async Task ShouldDenyIdentityOnlyProgramAdmissionGivenInactiveTenant()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.UseIdentityOnly();
        fixture.Source.Set(AccessAdmissionStableRead.Slot.Tenant, new Tenant(fixture.TenantId));

        // Act
        var result = await fixture.AdmitAsync();

        // Assert
        Assert.Null(result);
    }

    sealed class Fixture
    {
        readonly Member _member;
        AccessAdmissionSemanticEvaluator.Credential _credential;
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public ClaimsPrincipal Actor { get; }
        public Source Source { get; } = new();
        public Routes Routes { get; }
        public SelectionRoutes? SelectionRoutes { get; private set; }
        public AccessAdmissionAuthority Authority { get; private set; }
        public AccessAdmissionAuthority SelectionAuthority =>
            new(new Credentials(_credential), null, Source, new Clock(), "v1", Digest, selectionRoutes: SelectionRoutes);
        public string Target => new ComplianceProgram(TenantId, ProgramId).Stream.ToString();

        public Fixture()
        {
            var identity = new UserIdentity("provider", "subject");
            Assert.True(identity.Register(UserId, null).IsSuccess);
            var sessionId = Uuid.CreateVersion4();
            Actor = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", UserId.ToString()), new Claim("iss", "bdgrz")], "test"));
            var tenant = new Tenant(TenantId);
            Assert.True(tenant.Register(UserId, "Example", "example", creatorIsAdministrator: true).IsSuccess);
            Assert.True(tenant.ConfirmSlug("example").IsSuccess);
            Assert.True(tenant.Activate(UserId, null).IsSuccess);
            var member = new Member(TenantId, UserId);
            Assert.True(member.Register("client_personnel").IsSuccess);
            _member = member;
            var designation = Uuid.CreateVersion4();
            var primary = new PrimaryOrgAdmins(TenantId, designation);
            Assert.True(primary.Designate(designation, UserId, member.MembershipEpisodeId).IsSuccess);
            Assert.True(member.MarkPrimary(designation, member.MembershipEpisodeId).IsSuccess);
            Assert.True(primary.RecordOutcome(designation, UserId, member.MembershipEpisodeId, null).IsSuccess);
            var path = new List<AccessAdmissionStableRead.Participant>();
            Source.Add(path, AccessAdmissionStableRead.Slot.Identity, identity.Stream.ToString(), identity);
            Source.Add(path, AccessAdmissionStableRead.Slot.Policy, "stream://bdgrz/policy/current",
                new AccessAdmissionSemanticEvaluator.Policy("v1", Digest, true, true, true, true));
            Source.Add(path, AccessAdmissionStableRead.Slot.Session, "stream://bdgrz/sessions/session",
                new AccessAdmissionSemanticEvaluator.Session(UserId, identity.Id, sessionId, true));
            Source.Add(path, AccessAdmissionStableRead.Slot.Account, "stream://bdgrz/accounts/user",
                new AccessAdmissionSemanticEvaluator.ActorVeto(UserId, false));
            Source.Add(path, AccessAdmissionStableRead.Slot.GlobalVeto, "stream://bdgrz/veto/user",
                new AccessAdmissionSemanticEvaluator.ActorVeto(UserId, false));
            Source.Add(path, AccessAdmissionStableRead.Slot.Tenant, tenant.Stream.ToString(), tenant);
            Source.Add(path, AccessAdmissionStableRead.Slot.Member, member.Stream.ToString(), member);
            Source.Add(path, AccessAdmissionStableRead.Slot.Primary, primary.Stream.ToString(), primary);
            var credential = new AccessAdmissionSemanticEvaluator.Credential(UserId, identity.Id, sessionId,
                UserId.ToString(), "bdgrz", At.AddMinutes(-1), At.AddMinutes(1));
            _credential = credential;
            Routes = new Routes(new ProgramAdmissionPath(TenantId, UserId, true, Uuid.Empty, Uuid.Empty, "route-v1", true, path));
            Authority = new AccessAdmissionAuthority(new Credentials(credential), null, Source, new Clock(), "v1", Digest, Routes);
        }

        public ValueTask<AccessAdmissionEvidence?> AdmitAsync() =>
            Authority.AdmitProgramCreationAsync(new RequestDispatchContext(Actor), TenantId, ProgramId, Target, Digest);

        public void UseIdentityOnly()
        {
            ((ClaimsIdentity)Actor.Identity!).AddClaim(new Claim("user_identity_id", _credential.IdentityId.ToString()));
            _credential = _credential with
            {
                SessionId = Uuid.Empty,
                Scheme = AccessAdmissionSemanticEvaluator.CredentialScheme.BdgrzIdentityOnly,
                ExactIdentityBindingCertified = true
            };
            Routes.Path = Routes.Path with
            {
                Participants = Routes.Path.Participants.Where(participant => participant.Role is not
                    (AccessAdmissionStableRead.Slot.Session or AccessAdmissionStableRead.Slot.Account or AccessAdmissionStableRead.Slot.GlobalVeto)).ToArray()
            };
            Source.Set(AccessAdmissionStableRead.Slot.Policy, new AccessAdmissionSemanticEvaluator.Policy("v1", Digest,
                true, false, false, false, true));
            Authority = new AccessAdmissionAuthority(new Credentials(_credential), null, Source, new Clock(), "v1", Digest, Routes);
        }

        public void EnableSelection(bool organizationScope = true)
        {
            var route = Routes.Path;
            var scope = route.Primary ? null : new AccessGrantScope(
                organizationScope ? AccessGrantScopeKind.Organization : AccessGrantScopeKind.Program,
                organizationScope ? TenantId : ProgramId);
            SelectionRoutes = new SelectionRoutes(new ProgramSelectionAdmissionPath(TenantId, ProgramId, UserId,
                route.Primary, route.TeamId, route.RoleId, scope, route.RoutingGeneration, route.AlternativesResolved, route.Participants));
        }

        public ValueTask<AccessAdmissionEvidence?> AdmitSelectionAsync(Uuid operationId) =>
            SelectionAuthority.AdmitProgramSelectionAsync(new RequestDispatchContext(Actor), TenantId, ProgramId, operationId, Target, Digest);

        public void UseNonPrimaryMemberEpisode()
        {
            var member = new Member(TenantId, UserId);
            Assert.True(member.Register("client_personnel").IsSuccess);
            Source.Set(AccessAdmissionStableRead.Slot.Member, member);
        }

        public ValueTask<AccessAdmissionSemanticEvaluator.Decision> EvaluateExcludedRoleAsync()
        {
            var route = Routes.Path;
            var candidate = new AccessAdmissionSemanticEvaluator.Candidate(_credential, Uuid.CreateVersion4(),
                Target, Digest, route.Participants);
            var program = new AccessAdmissionSemanticEvaluator.ProgramCandidate(candidate, TenantId, route.TeamId,
                BuiltInRbac.AdvisorRoleId(TenantId), false, ProgramId, new(AccessGrantScopeKind.Program, ProgramId));
            return new AccessAdmissionSemanticEvaluator(new AccessAdmissionStableRead(Source, new Clock()), "v1", Digest)
                .EvaluateProgramSelectionAsync(program);
        }

        public void UseTeamPath(bool organizationScope = true)
        {
            var teamId = Uuid.CreateVersion4();
            var roleId = Uuid.CreateVersion4();
            var actor = new ActorAttribution(UserId.ToString(), "bdgrz");
            var team = new Team(TenantId, teamId);
            var role = new Role(TenantId, roleId);
            var membership = new TeamMember(TenantId, teamId, _member.Id);
            Assert.True(team.Define("Reviewers").IsSuccess);
            Assert.True(role.Define("Program managers").IsSuccess);
            Assert.True(membership.Assign(_member.MembershipEpisodeId).IsSuccess);
            var assignmentId = Uuid.CreateVersion4();
            var assignment = new TeamRole(TenantId, teamId, roleId, assignmentId,
                new AccessGrantScope(organizationScope ? AccessGrantScopeKind.Organization : AccessGrantScopeKind.Program,
                    organizationScope ? TenantId : ProgramId));
            Assert.True(assignment.Assign(assignmentId, 0, actor).IsSuccess);
            var permissionId = Uuid.CreateVersion4();
            var permission = new RolePermission(TenantId, roleId, RbacPermissions.ProgramManage, permissionId);
            Assert.True(permission.Assign(permissionId, 0, actor).IsSuccess);
            var path = Routes.Path.Participants.Where(x => x.Role != AccessAdmissionStableRead.Slot.Primary).ToList();
            Source.Add(path, AccessAdmissionStableRead.Slot.TeamMember, membership.Stream.ToString(), membership);
            Source.Add(path, AccessAdmissionStableRead.Slot.Team, team.Stream.ToString(), team);
            Source.Add(path, AccessAdmissionStableRead.Slot.TeamRole, assignment.Stream.ToString(), assignment);
            Source.Add(path, AccessAdmissionStableRead.Slot.Role, role.Stream.ToString(), role);
            Source.Add(path, AccessAdmissionStableRead.Slot.RolePermission, permission.Stream.ToString(), permission);
            Routes.Path = new ProgramAdmissionPath(TenantId, UserId, false, teamId, roleId, "team-route-v1", true, path);
        }
    }

    sealed class Credentials(AccessAdmissionSemanticEvaluator.Credential value) : ITrustedAdmissionCredentialResolver
    {
        public bool IsQualified => true;
        public ValueTask<AccessAdmissionSemanticEvaluator.Credential?> ResolveAsync(IRequestContext context, CancellationToken ct) =>
            ValueTask.FromResult<AccessAdmissionSemanticEvaluator.Credential?>(value);
    }

    sealed class Routes(ProgramAdmissionPath path) : ITrustedAdmissionProgramPathResolver
    {
        public ProgramAdmissionPath Path { get; set; } = path;
        public bool Qualified { get; set; } = true;
        public bool Current { get; set; } = true;
        public bool Missing { get; set; }
        public bool Checked { get; private set; }
        public bool IsQualified => Qualified;
        public ValueTask<ProgramAdmissionPath?> ResolveAsync(AccessAdmissionSemanticEvaluator.Credential actor,
            Uuid tenantId, CancellationToken ct) => ValueTask.FromResult<ProgramAdmissionPath?>(Missing ? null : Path);
        public ValueTask<bool> IsCurrentAsync(ProgramAdmissionPath captured, CancellationToken ct)
        {
            Checked = true;
            return ValueTask.FromResult(Current && captured.RoutingGeneration == Path.RoutingGeneration && captured.Participants.SequenceEqual(Path.Participants));
        }
    }

    sealed class SelectionRoutes(ProgramSelectionAdmissionPath path) : ITrustedAdmissionProgramSelectionPathResolver
    {
        public ProgramSelectionAdmissionPath Path { get; set; } = path;
        public bool Qualified { get; set; } = true;
        public bool Current { get; set; } = true;
        public bool Missing { get; set; }
        public bool Checked { get; private set; }
        public bool IsQualified => Qualified;
        public ValueTask<ProgramSelectionAdmissionPath?> ResolveAsync(AccessAdmissionSemanticEvaluator.Credential actor,
            Uuid tenantId, Uuid programId, CancellationToken ct) =>
            ValueTask.FromResult<ProgramSelectionAdmissionPath?>(Missing ? null : Path);
        public ValueTask<bool> IsCurrentAsync(ProgramSelectionAdmissionPath captured, CancellationToken ct)
        {
            Checked = true;
            return ValueTask.FromResult(Current && captured.RoutingGeneration == Path.RoutingGeneration &&
                captured.Scope == Path.Scope && captured.Participants.SequenceEqual(Path.Participants));
        }
    }

    sealed class Source : AccessAdmissionStableRead.ISource
    {
        readonly Dictionary<AccessAdmissionStableRead.Slot, string> _streams = [];
        readonly Dictionary<string, AccessAdmissionStableRead.Observation> _values = [];
        public bool IsFreshnessQualified => true;
        public int Reads { get; private set; }
        public int ChangeOnRead { get; set; }
        public Action? OnRead { get; set; }
        public void Add(List<AccessAdmissionStableRead.Participant> path, AccessAdmissionStableRead.Slot slot, string stream, object state)
        {
            path.Add(new(slot, stream));
            _streams.Add(slot, stream);
            Set(slot, state);
        }
        public void Set(AccessAdmissionStableRead.Slot slot, object state)
        {
            var stream = _streams[slot];
            _values[stream] = new(new(stream, "generation", 1), state);
        }
        public void Remove(AccessAdmissionStableRead.Slot slot) => _values.Remove(_streams[slot]);
        public ValueTask<AccessAdmissionStableRead.Observation?> ReadCurrentAsync(string stream, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Reads++;
            OnRead?.Invoke();
            var value = _values.GetValueOrDefault(stream);
            if (Reads == ChangeOnRead && value is not null)
                value = value with { Version = value.Version with { Position = value.Version.Position + 1 } };
            return ValueTask.FromResult(value);
        }
    }

    sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => At;
    }
}
