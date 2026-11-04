using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Authorizers;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.Workforce.Contracts;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Services;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Workforce.Tests;

public sealed class ServiceIdentityAuthorizationTests
{
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    static readonly DateOnly Today = new(2026, 11, 4);
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");

    [Theory]
    [InlineData("record", ServiceIdentityPermissions.Record, true)]
    [InlineData("record", RbacPermissions.WorkforceManage, false)]
    [InlineData("owner", ServiceIdentityPermissions.Manage, true)]
    [InlineData("owner", RbacPermissions.WorkforceManage, false)]
    [InlineData("review", ServiceIdentityPermissions.Review, true)]
    [InlineData("review", RbacPermissions.WorkforceManage, false)]
    public async Task ShouldRequireSpecificGrantGivenNamedIdentityAction(string action, string grant, bool accepted)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Request(action);
        var authorizer = new WorkforceAuthorizer(new FixedMembershipDirectory(true), new Active(), new Permissions(grant));

        // Act
        var result = await authorizer.AuthorizeAsync(fixture.Context(request), CancellationToken.None);

        // Assert
        Assert.Equal(accepted, result.IsSuccess);
        if (!accepted)
            Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldAdmitOnlyLinkedPersonGivenOwnerReview(bool linked)
    {
        // Arrange
        var fixture = new Fixture(linked);
        var authorizer = new WorkforceAuthorizer(new FixedMembershipDirectory(true), new Active(),
            new Permissions(null), fixture);

        // Act
        var result = await authorizer.AuthorizeAsync(fixture.Context(fixture.Request("review")), CancellationToken.None);

        // Assert
        Assert.Equal(linked, result.IsSuccess);
        Assert.Single(new AggregateScenario<ServiceIdentity>(fixture.Identity).PendingEvents);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    public async Task ShouldRequireCurrentActiveTeamMembershipGivenTeamOwnerReview(
        bool sameEpisode, bool teamActive, bool accepted)
    {
        // Arrange
        var fixture = new Fixture(teamOwner: true, sameEpisode: sameEpisode, teamActive: teamActive);
        var authorizer = new WorkforceAuthorizer(new FixedMembershipDirectory(true), new Active(),
            new Permissions(null), fixture);

        // Act
        var result = await authorizer.AuthorizeAsync(fixture.Context(fixture.Request("review")), CancellationToken.None);

        // Assert
        Assert.Equal(accepted, result.IsSuccess);
        if (!accepted)
            Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Theory]
    [InlineData("direct")]
    [InlineData("mcp")]
    public async Task ShouldRefuseReviewGivenNonPersonalHttpInvocation(string transport)
    {
        // Arrange
        var fixture = new Fixture();
        var authorizer = new WorkforceAuthorizer(new FixedMembershipDirectory(true), new Active(),
            new Permissions(ServiceIdentityPermissions.Review), fixture);
        RequestInvocation invocation = transport == "mcp" ? new McpInvocation("synthetic.review") : new DirectInvocation();

        // Act
        var result = await authorizer.AuthorizeAsync(fixture.Context(fixture.Request("review"), invocation), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task ShouldPreserveTenantGateGivenSpecificServiceGrant(bool member, bool active)
    {
        // Arrange
        var fixture = new Fixture();
        var authorizer = new WorkforceAuthorizer(new FixedMembershipDirectory(member), new Active(active),
            new Permissions(ServiceIdentityPermissions.Manage), fixture);

        // Act
        var result = await authorizer.AuthorizeAsync(fixture.Context(fixture.Request("owner")), CancellationToken.None);

        // Assert
        Assert.Equal(member ? RequestErrorKind.Forbidden : RequestErrorKind.NotFound,
            Assert.IsType<RequestError>(result.Error).Kind);
    }

    sealed class Fixture : IAggregateReader
    {
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid User { get; } = Uuid.CreateVersion4();
        public Uuid Owner { get; } = Uuid.CreateVersion4();
        public ServiceIdentity Identity { get; }
        readonly Person _person;
        readonly Team? _team;
        readonly TeamMember? _assignment;
        readonly Member? _member;

        public Fixture(bool linked = false, bool teamOwner = false, bool sameEpisode = true, bool teamActive = true)
        {
            Identity = new ServiceIdentity(Tenant, Uuid.CreateVersion4());
            Assert.True(Identity.Record(new ServiceIdentityTerms("Bot", "bot", "Approved purpose", null,
                "active", teamOwner ? "team" : "person", Owner, Today.AddMonths(6)), Today, Author, Now).IsSuccess);
            DomainEvent[] facts =
            [
                DomainEventSeed.Attach(new PersonRecorded(Tenant, Owner, "Owner", null, Author, Now), Owner, 1),
                DomainEventSeed.Attach(new PersonMembershipCorrelated(Tenant, Owner, 2,
                    linked ? User : Uuid.CreateVersion4(), Author, Now), Owner, 2),
            ];
            _person = NativeFacts.Replay(new Person(Tenant, Owner), facts);
            if (teamOwner)
            {
                _team = new Team(Tenant, Owner);
                Assert.True(_team.Define(NativeFacts.Root(_team, Owner, "DefineTeam", null), NativeFacts.Ref(), "Owner team").IsSuccess);
                if (!teamActive)
                    Assert.True(_team.Delete(NativeFacts.Root(_team, Uuid.CreateVersion4(), "DeleteTeam", null), NativeFacts.Ref()).IsSuccess);
                _member = new Member(Tenant, User);
                var episode = Uuid.CreateVersion4();
                Assert.True(_member.Register(NativeFacts.Root(_member, episode, "RegisterMember", null), NativeFacts.Ref()).IsSuccess);
                _assignment = new TeamMember(Tenant, Owner, RbacIds.Member(Tenant, User));
                Assert.True(_assignment.Assign(NativeFacts.Root(_assignment, Uuid.CreateVersion4(), "AssignTeamMember", 0),
                    NativeFacts.Ref(), sameEpisode ? episode : Uuid.CreateVersion4(), 0, 0).IsSuccess);
            }
        }

        public IWorkforceRequest Request(string action) => action switch
        {
            "record" => new RecordServiceIdentity(Tenant, "Bot", "bot", "Approved purpose", "person", Owner, Today.AddMonths(6)),
            "owner" => new ChangeServiceIdentityOwner(Tenant, Identity.Id, 1, "person", Owner),
            "review" => new ReviewServiceIdentity(Tenant, Identity.Id, 1, Today.AddYears(1)),
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };

        public RequestContext<IWorkforceRequest> Context(IWorkforceRequest request, RequestInvocation? invocation = null) =>
            new(request, new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "bdgrz"),
                new Claim("sub", User.ToString())], "BdgrzSession")), invocation ??
                new HttpInvocation("POST", "/synthetic/review", "/synthetic/review", "synthetic"));

        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
            where TAggregate : Aggregate => ValueTask.FromResult(aggregate switch
            {
                ServiceIdentity when aggregate.Id == Identity.Id => (TAggregate)(Aggregate)Identity,
                Person when aggregate.Id == Owner => (TAggregate)(Aggregate)_person,
                Team when _team is not null && aggregate.Id == _team.Id => (TAggregate)(Aggregate)_team,
                TeamMember when _assignment is not null && aggregate.Id == _assignment.Id => (TAggregate)(Aggregate)_assignment,
                Member when _member is not null && aggregate.Id == _member.Id => (TAggregate)(Aggregate)_member,
                _ => aggregate,
            });
    }

    sealed class Permissions(string? grant) : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(permission == grant);
    }

    sealed class Active(bool active = true) : ITenantActivity
    {
        public ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default) => ValueTask.FromResult(active);
    }
}
