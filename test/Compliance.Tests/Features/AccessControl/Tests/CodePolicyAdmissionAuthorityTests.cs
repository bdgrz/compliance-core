using System.Runtime.CompilerServices;
using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class CodePolicyAdmissionAuthorityTests
{
    const string Digest = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    static readonly DateTimeOffset At = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldRecordRealOwnerPositionsGivenConfirmedPrimary()
    {
        // Arrange
        var fixture = new Fixture();
        var operation = Uuid.CreateVersion4();
        var target = new ComplianceProgram(fixture.TenantId, fixture.ProgramId).Stream.ToString();

        // Act
        var result = await fixture.Authority.AdmitProgramSelectionAsync(fixture.Context, fixture.TenantId,
            fixture.ProgramId, operation, target, Digest);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.FormatVersion);
        Assert.Null(result.Policy);
        Assert.Equal(CodePolicyAdmissionAuthority.PolicyDigest, result.PolicyContentSha256);
        Assert.Equal(fixture.PrimaryStream, result.Primary!.Stream);
        Assert.Equal(fixture.IdentityStream, result.Identity.Stream);
        Assert.True(result.Identity.Position > 0);
        Assert.True(AccessAdmissionEvidenceBinding.BindsProgramSelection(result, fixture.UserId,
            new ActorAttribution(fixture.UserId.ToString(), "bdgrz"), operation, target, Digest));
    }

    [Fact]
    public async Task ShouldUseExplicitDevelopmentPolicyGivenConfiguredDevelopmentOperatorBypass()
    {
        // Arrange
        var fixture = new Fixture(developer: true);
        var operation = Uuid.CreateVersion4();
        var target = "stream://bdgrz/criteria-test/edition";

        // Act
        var result = await fixture.Authority.AdmitCriteriaOperatorAsync(fixture.Context, operation, target, Digest);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.Operator);
        Assert.Equal(CodePolicyAdmissionAuthority.DeveloperPolicyGeneration, result.PolicyGeneration);
        Assert.True(AccessAdmissionEvidenceBinding.BindsCriteriaOperator(result, fixture.UserId,
            new ActorAttribution(fixture.UserId.ToString(), "bdgrz"), operation, target, Digest));
        Assert.False(AccessAdmissionEvidenceBinding.BindsCriteriaOperator(result with
        { PolicyGeneration = CodePolicyAdmissionAuthority.PolicyGeneration }, fixture.UserId,
            new ActorAttribution(fixture.UserId.ToString(), "bdgrz"), operation, target, Digest));
    }

    [Fact]
    public async Task ShouldRemainUnavailableGivenMissingOperatorSource()
    {
        // Arrange
        var fixture = new Fixture();

        // Act
        // Assert
        await Assert.ThrowsAsync<AccessAdmissionUnavailableException>(async () =>
            await fixture.Authority.AdmitCriteriaOperatorAsync(fixture.Context, Uuid.CreateVersion4(),
                "stream://bdgrz/criteria-test/edition", Digest));
    }

    [Fact]
    public async Task ShouldAdmitProgramGrantGivenOrganizationWideCreationIsNotGranted()
    {
        // Arrange
        var fixture = new Fixture(team: true);
        var target = new ComplianceProgram(fixture.TenantId, fixture.ProgramId).Stream.ToString();
        var operation = Uuid.CreateVersion4();

        // Act
        var selected = await fixture.Authority.AdmitProgramSelectionAsync(fixture.Context, fixture.TenantId,
            fixture.ProgramId, operation, target, Digest);
        var creation = await Record.ExceptionAsync(async () => await fixture.Authority.AdmitProgramCreationAsync(
            fixture.Context, fixture.TenantId, fixture.ProgramId, target, Digest));

        // Assert
        Assert.NotNull(selected);
        Assert.NotNull(selected.TeamRole);
        Assert.NotNull(selected.RolePermission);
        Assert.Null(selected.Primary);
        Assert.IsType<AccessAdmissionUnavailableException>(creation);
    }

    sealed class Fixture
    {
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public IRequestContext Context { get; }
        public CodePolicyAdmissionAuthority Authority { get; }
        public string PrimaryStream { get; }
        public string IdentityStream { get; }
        readonly Memory _memory = new();

        public Fixture(bool developer = false, bool team = false)
        {
            var identity = new UserIdentity("provider", "subject");
            var identityScenario = new AggregateScenario<UserIdentity>(identity);
            Assert.True(identity.Register(UserId, null).IsSuccess);
            _memory.Add(identity.Stream, identityScenario.PendingEvents);
            IdentityStream = identity.Stream.ToString();
            var tenant = new Tenant(TenantId);
            var tenantScenario = new AggregateScenario<Tenant>(tenant);
            Assert.True(tenant.Register(UserId, "Example", "example", creatorIsAdministrator: true).IsSuccess);
            Assert.True(tenant.ConfirmSlug("example").IsSuccess);
            Assert.True(tenant.Activate(UserId, null).IsSuccess);
            _memory.Add(tenant.Stream, tenantScenario.PendingEvents);
            var member = new Member(TenantId, UserId);
            var memberScenario = new AggregateScenario<Member>(member);
            Assert.True(member.Register("client_personnel").IsSuccess);
            var designation = Uuid.CreateVersion4();
            var primary = new PrimaryOrgAdmins(TenantId, designation);
            var primaryScenario = new AggregateScenario<PrimaryOrgAdmins>(primary);
            if (!team)
            {
                Assert.True(primary.Designate(designation, UserId, member.MembershipEpisodeId).IsSuccess);
                Assert.True(member.MarkPrimary(designation, member.MembershipEpisodeId).IsSuccess);
                Assert.True(primary.RecordOutcome(designation, UserId, member.MembershipEpisodeId, null).IsSuccess);
                _memory.Add(primary.Stream, primaryScenario.PendingEvents);
            }
            _memory.Add(member.Stream, memberScenario.PendingEvents);
            PrimaryStream = primary.Stream.ToString();
            if (team)
            {
                var teamId = Uuid.CreateVersion4();
                var roleId = Uuid.CreateVersion4();
                var actorAttribution = new ActorAttribution(UserId.ToString(), "bdgrz");
                var group = new Team(TenantId, teamId);
                var groupScenario = new AggregateScenario<Team>(group);
                Assert.True(group.Define("Reviewers").IsSuccess);
                _memory.Add(group.Stream, groupScenario.PendingEvents);
                var role = new Role(TenantId, roleId);
                var roleScenario = new AggregateScenario<Role>(role);
                Assert.True(role.Define("Program managers").IsSuccess);
                _memory.Add(role.Stream, roleScenario.PendingEvents);
                var membership = new TeamMember(TenantId, teamId, member.Id);
                var membershipScenario = new AggregateScenario<TeamMember>(membership);
                Assert.True(membership.Assign(member.MembershipEpisodeId).IsSuccess);
                _memory.Add(membership.Stream, membershipScenario.PendingEvents);
                var id = Uuid.CreateVersion4();
                var assignment = new TeamRole(TenantId, teamId, roleId, id, new(AccessGrantScopeKind.Program, ProgramId));
                var assignmentScenario = new AggregateScenario<TeamRole>(assignment);
                Assert.True(assignment.Assign(id, 0, actorAttribution).IsSuccess);
                _memory.Add(assignment.Stream, assignmentScenario.PendingEvents);
                var permissionId = Uuid.CreateVersion4();
                var permission = new RolePermission(TenantId, roleId, RbacPermissions.ProgramManage, permissionId);
                var permissionScenario = new AggregateScenario<RolePermission>(permission);
                Assert.True(permission.Assign(permissionId, 0, actorAttribution).IsSuccess);
                _memory.Add(permission.Stream, permissionScenario.PendingEvents);
            }
            var actor = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", UserId.ToString()), new Claim("iss", "bdgrz")], "test"));
            Context = new RequestDispatchContext(actor);
            var credential = new AccessAdmissionSemanticEvaluator.Credential(UserId, identity.Id, Uuid.Empty,
                UserId.ToString(), "bdgrz", At.AddMinutes(-1), At.AddMinutes(1),
                AccessAdmissionSemanticEvaluator.CredentialScheme.BdgrzIdentityOnly, true);
            Authority = new CodePolicyAdmissionAuthority(new Credential(credential), _memory, _memory,
                new FixedClock(), new DeveloperUserRegistration(developer));
        }
    }

    sealed class Credential(AccessAdmissionSemanticEvaluator.Credential value) : ITrustedAdmissionCredentialResolver
    {
        public bool IsQualified => true;
        public ValueTask<AccessAdmissionSemanticEvaluator.Credential?> ResolveAsync(IRequestContext context, CancellationToken ct) =>
            ValueTask.FromResult<AccessAdmissionSemanticEvaluator.Credential?>(value);
    }

    sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => At; }

    sealed class Memory : IAggregateReader, IDomainEventReader
    {
        readonly Dictionary<EventStreamAddress, DomainEvent[]> _events = [];
        public void Add(EventStreamAddress stream, IReadOnlyList<DomainEvent> values) => _events.Add(stream, values.ToArray());
        public ValueTask<T> HydrateAsync<T>(T aggregate, CancellationToken ct = default) where T : Aggregate
        {
            ct.ThrowIfCancellationRequested();
            if (_events.TryGetValue(aggregate.Stream, out var facts))
                new AggregateScenario<T>(aggregate).Given(facts);
            return ValueTask.FromResult(aggregate);
        }
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong fromVersion,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            if (!_events.TryGetValue(stream, out var facts))
                yield break;
            for (var index = fromVersion; index < (ulong)facts.Length; index++)
            {
                ct.ThrowIfCancellationRequested();
                yield return new DomainEventRecord(stream, facts[(int)index], index, EventCursor.Start);
            }
        }
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor after,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            foreach (var (stream, facts) in _events)
                for (var index = 0; index < facts.Length; index++)
                {
                    ct.ThrowIfCancellationRequested();
                    yield return new DomainEventRecord(stream, facts[index], (ulong)index, EventCursor.Start);
                }
        }
    }
}
