using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Authorizers;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Handlers;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Services;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Workforce.Tests;

public sealed class ServiceIdentityReviewMembershipTests
{
    static readonly DateOnly Today = new(2026, 11, 4);
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("person", "suspended", false)]
    [InlineData("person", "suspended", true)]
    [InlineData("person", "deprovisioned", false)]
    [InlineData("person", "deprovisioned", true)]
    [InlineData("person", "expired_guest", false)]
    [InlineData("person", "expired_guest", true)]
    [InlineData("team", "suspended", false)]
    [InlineData("team", "suspended", true)]
    [InlineData("team", "deprovisioned", false)]
    [InlineData("team", "deprovisioned", true)]
    [InlineData("team", "expired_guest", false)]
    [InlineData("team", "expired_guest", true)]
    public async Task ShouldRefuseOwnerReviewWithoutFactGivenIneligibleCommittedMemberAndActiveProjection(
        string ownerKind, string state, bool throughBus)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(ownerKind, state);
        var before = await fixture.Reader.HydrateAsync(new ServiceIdentity(fixture.Tenant, fixture.IdentityId));
        var memberBefore = await fixture.Reader.HydrateAsync(new Member(fixture.Tenant, fixture.User));
        var projected = await fixture.Memberships.GetAsync(fixture.Tenant.ToString(), fixture.User);
        Assert.NotNull(projected);
        Assert.False(projected.IsSuspended);
        Assert.False(projected.IsDeprovisioned);
        Assert.False(memberBefore.IsAccessEligibleOn(Today));
        var membershipFacts = await fixture.FactsAsync(memberBefore.Stream);
        if (state == "suspended")
            Assert.IsType<MemberSuspended>(membershipFacts[^1]);
        else if (state == "deprovisioned")
            Assert.IsType<MemberDeprovisioned>(membershipFacts[^1]);
        else
            Assert.Equal(Today.AddDays(-1), Assert.IsType<MemberRegistered>(membershipFacts[^1]).MembershipExpiresOn);

        // Act
        var result = await fixture.ReviewAsync(throughBus);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(result.Error).Kind);
        var after = await fixture.Reader.HydrateAsync(new ServiceIdentity(fixture.Tenant, fixture.IdentityId));
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(before.Revision, after.Revision);
        Assert.DoesNotContain(await fixture.FactsAsync(after.Stream), fact => fact is ServiceIdentityReviewed);
        var memberAfter = await fixture.Reader.HydrateAsync(new Member(fixture.Tenant, fixture.User));
        Assert.Equal(memberBefore.Version, memberAfter.Version);
    }

    [Theory]
    [InlineData("person", false, false)]
    [InlineData("person", false, true)]
    [InlineData("team", false, false)]
    [InlineData("team", false, true)]
    [InlineData("person", true, false)]
    [InlineData("person", true, true)]
    public async Task ShouldRecordActualReviewerGivenEligibleOwnerOrLead(
        string ownerKind, bool lead, bool throughBus)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(ownerKind, "active", lead);

        // Act
        var result = await fixture.ReviewAsync(throughBus);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var identity = await fixture.Reader.HydrateAsync(new ServiceIdentity(fixture.Tenant, fixture.IdentityId));
        Assert.Equal(2, identity.Revision);
        var review = Assert.IsType<ServiceIdentityReviewed>((await fixture.FactsAsync(identity.Stream))[^1]);
        Assert.Equal(RbacIds.Member(fixture.Tenant, fixture.User).ToString(), review.Reviewer.Id);
        Assert.Equal(Now, review.ReviewedAt);
    }

    [Theory]
    [InlineData("person", false)]
    [InlineData("person", true)]
    [InlineData("team", false)]
    [InlineData("team", true)]
    public async Task ShouldAllowOwnerReviewGivenGuestStillEligibleOnExpiryDate(string ownerKind, bool throughBus)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(ownerKind, "guest_expires_today");

        // Act
        var result = await fixture.ReviewAsync(throughBus);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var facts = await fixture.FactsAsync(new ServiceIdentity(fixture.Tenant, fixture.IdentityId).Stream);
        Assert.IsType<ServiceIdentityReviewed>(facts[^1]);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly InMemoryEventStore _events;
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid User { get; } = Uuid.CreateVersion4();
        public Uuid IdentityId { get; } = Uuid.CreateVersion4();
        public FixedMembershipDirectory Memberships { get; } = new(true);
        public IAggregateReader Reader => _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        IAggregateWriter Writer => _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();

        Fixture(bool lead)
        {
            _events = new InMemoryEventStore();
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(_events);
            services.AddSingleton<TimeProvider>(new Clock());
            services.AddSingleton<ITenantMembershipDirectoryReader>(Memberships);
            services.AddSingleton<ITenantActivity>(new Active());
            services.AddSingleton<IPermissionAuthorizer>(new Permissions(lead));
            services.AddPortia().AddRequestAuthorizer<WorkforceAuthorizer>()
                .AddRequestHandler<ReviewServiceIdentityHandler>();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
        }

        public static async Task<Fixture> CreateAsync(string ownerKind, string state, bool lead = false)
        {
            var fixture = new Fixture(lead);
            try
            {
                await fixture.SeedAsync(ownerKind, state, lead);
                return fixture;
            }
            catch
            {
                await fixture.DisposeAsync();
                throw;
            }
        }

        async Task SeedAsync(string ownerKind, string state, bool lead)
        {
            var member = new Member(Tenant, User);
            var expiresOn = state switch
            {
                "expired_guest" => (DateOnly?)Today.AddDays(-1),
                "guest_expires_today" => Today,
                _ => null,
            };
            Assert.True(member.Register(expiresOn is null ? "client_personnel" : "guest", expiresOn).IsSuccess);
            await Writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));
            member = await Reader.HydrateAsync(new Member(Tenant, User));
            if (state == "suspended")
            {
                Assert.True(member.Suspend(Uuid.CreateVersion4(), "Administrator", Now, "Access revoked.").IsSuccess);
                await Writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));
            }
            else if (state == "deprovisioned")
            {
                Assert.True(member.Deprovision(Uuid.CreateVersion4(), "Administrator", Now, "Access revoked.").IsSuccess);
                await Writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));
            }

            var ownerId = Uuid.CreateVersion4();
            var author = ActorReference.ForMember(RbacIds.Member(Tenant, User), "Owner");
            if (ownerKind == "person")
            {
                DomainEvent[] facts =
                [
                    DomainEventSeed.Attach(new PersonRecorded(Tenant, ownerId, "Owner", null, author, Now), ownerId, 1),
                    DomainEventSeed.Attach(new PersonMembershipCorrelated(Tenant, ownerId, 2,
                        lead ? Uuid.CreateVersion4() : User, author, Now), ownerId, 2),
                ];
                await _events.AppendAsync(new Person(Tenant, ownerId).Stream, 0, facts);
            }
            else
            {
                var team = new Team(Tenant, ownerId);
                Assert.True(team.Define(NativeFacts.Root(team, ownerId, "DefineTeam", null),
                    NativeFacts.Ref(), "Owner team").IsSuccess);
                await Writer.SaveAsync(team, new RequestDispatchContext(RequestActor.System));
                var assignment = new TeamMember(Tenant, ownerId, member.Id);
                Assert.True(assignment.Assign(NativeFacts.Root(assignment, Uuid.CreateVersion4(), "AssignTeamMember", 0),
                    NativeFacts.Ref(), member.MembershipEpisodeId, 0, 0).IsSuccess);
                await Writer.SaveAsync(assignment, new RequestDispatchContext(RequestActor.System));
            }
            var identity = new ServiceIdentity(Tenant, IdentityId);
            Assert.True(identity.Record(new ServiceIdentityTerms("Bot", "bot", "Approved purpose", null,
                "active", ownerKind, ownerId, Today.AddMonths(6)), Today, author, Now).IsSuccess);
            await Writer.SaveAsync(identity, new RequestDispatchContext(RequestActor.System));
        }

        public async Task<Result> ReviewAsync(bool throughBus)
        {
            var request = new ReviewServiceIdentity(Tenant, IdentityId, 1, Today.AddYears(1), "Annual review");
            var context = new RequestDispatchContext(new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("iss", "bdgrz"), new Claim("sub", User.ToString())], "BdgrzSession")),
                new HttpInvocation("POST", "/synthetic/review", "/synthetic/review", "synthetic"));
            if (throughBus)
                return await _scope.ServiceProvider.GetRequiredService<IRequestBus>()
                    .DispatchAsync(request, context, CancellationToken.None);
            var handler = new ReviewServiceIdentityHandler(_scope.ServiceProvider.GetRequiredService<IAggregateExecutor>(),
                Reader, _scope.ServiceProvider.GetRequiredService<IPermissionAuthorizer>(), new Clock());
            return await handler.HandleAsync(new Cntryl.Portia.RequestContext<ReviewServiceIdentity>(request, context), CancellationToken.None);
        }

        public async Task<List<DomainEvent>> FactsAsync(EventStreamAddress stream)
        {
            var facts = new List<DomainEvent>();
            await foreach (var record in _events.ReadAsync(stream, 0))
                if (!record.Event.Metadata.IsAudit)
                    facts.Add(record.Event);
            return facts;
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class Permissions(bool lead) : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(lead && permission == ServiceIdentityPermissions.Review);
    }

    sealed class Active : ITenantActivity
    {
        public ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default) => ValueTask.FromResult(true);
    }

    sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
