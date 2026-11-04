using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Handlers;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Services;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Workforce.Tests;

public sealed class ServiceIdentityNamedHandlerTests
{
    static readonly DateOnly Today = new(2026, 11, 4);
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRecordActualReviewerGivenCurrentOwnerOrSpecificReviewGrant(bool grant)
    {
        // Arrange
        var fixture = new Fixture(linked: !grant);
        var handler = new ReviewServiceIdentityHandler(fixture.Executor, fixture, new Permissions(grant), new Clock());

        // Act
        var result = await handler.HandleAsync(fixture.Context(new ReviewServiceIdentity(fixture.Tenant,
            fixture.Identity.Id, 1, Today.AddYears(1), "Annual review")), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var review = Assert.IsType<ServiceIdentityReviewed>(fixture.Facts[^1]);
        Assert.Equal(RbacIds.Member(fixture.Tenant, fixture.User).ToString(), review.Reviewer.Id);
        Assert.Equal(Now, review.ReviewedAt);
        Assert.Equal(1, fixture.Executor.Calls);
    }

    [Theory]
    [InlineData("mcp")]
    [InlineData("direct")]
    public async Task ShouldRefuseWithoutFactGivenReviewOutsidePersonalHttp(string transport)
    {
        // Arrange
        var fixture = new Fixture(linked: true);
        var handler = new ReviewServiceIdentityHandler(fixture.Executor, fixture, new Permissions(true), new Clock());
        RequestInvocation invocation = transport == "mcp" ? new McpInvocation("synthetic.review") : new DirectInvocation();

        // Act
        var result = await handler.HandleAsync(fixture.Context(new ReviewServiceIdentity(fixture.Tenant,
            fixture.Identity.Id, 1, Today.AddYears(1)), invocation), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Equal(0, fixture.Executor.Calls);
        Assert.Single(fixture.Facts);
    }

    [Fact]
    public async Task ShouldRefuseWithoutFactGivenUnrelatedReviewerWithoutSpecificGrant()
    {
        // Arrange
        var fixture = new Fixture();
        var handler = new ReviewServiceIdentityHandler(fixture.Executor, fixture, new Permissions(false), new Clock());

        // Act
        var result = await handler.HandleAsync(fixture.Context(new ReviewServiceIdentity(fixture.Tenant,
            fixture.Identity.Id, 1, Today.AddYears(1))), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Equal(0, fixture.Executor.Calls);
        Assert.Single(fixture.Facts);
    }

    [Fact]
    public async Task ShouldRefuseWithoutFactGivenPredictedFutureRevisionForOwnerReview()
    {
        // Arrange
        var fixture = new Fixture(linked: true);
        var handler = new ReviewServiceIdentityHandler(fixture.Executor, fixture, new Permissions(false), new Clock());

        // Act
        var result = await handler.HandleAsync(fixture.Context(new ReviewServiceIdentity(fixture.Tenant,
            fixture.Identity.Id, 2, Today.AddYears(1))), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Equal(0, fixture.Executor.Calls);
        Assert.Single(fixture.Facts);
    }

    [Fact]
    public async Task ShouldRefuseReviewWithoutFactGivenOwnerChangeAfterAuthorityRead()
    {
        // Arrange
        var fixture = new Fixture(linked: true);
        fixture.Executor.BeforeExecute = () => Assert.Null(fixture.Identity.ChangeOwner(1,
            "person", Uuid.CreateVersion4(), Author, Now));
        var handler = new ReviewServiceIdentityHandler(fixture.Executor, fixture, new Permissions(false), new Clock());

        // Act
        var result = await handler.HandleAsync(fixture.Context(new ReviewServiceIdentity(fixture.Tenant,
            fixture.Identity.Id, 1, Today.AddYears(1))), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Equal(2, fixture.Facts.Count);
        Assert.IsType<ServiceIdentityOwnerChanged>(fixture.Facts[^1]);
        Assert.DoesNotContain(fixture.Facts, fact => fact is ServiceIdentityReviewed);
    }

    [Fact]
    public async Task ShouldRefuseWithoutFactGivenNewOwnerNotRecorded()
    {
        // Arrange
        var fixture = new Fixture();
        var handler = new ChangeServiceIdentityOwnerHandler(fixture.Executor, fixture, new Clock());

        // Act
        var result = await handler.HandleAsync(fixture.Context(new ChangeServiceIdentityOwner(fixture.Tenant,
            fixture.Identity.Id, 1, "person", Uuid.CreateVersion4())), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Equal(0, fixture.Executor.Calls);
        Assert.Single(fixture.Facts);
    }

    sealed class Fixture : IAggregateReader
    {
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid User { get; } = Uuid.CreateVersion4();
        public Uuid Owner { get; } = Uuid.CreateVersion4();
        public ServiceIdentity Identity { get; }
        public OwnerExecutor Executor { get; }
        public IReadOnlyList<DomainEvent> Facts => new AggregateScenario<ServiceIdentity>(Identity).PendingEvents;
        readonly Person _person;
        readonly Member _member;

        public Fixture(bool linked = false)
        {
            Identity = new ServiceIdentity(Tenant, Uuid.CreateVersion4());
            Assert.True(Identity.Record(new ServiceIdentityTerms("Bot", "bot", "Approved purpose", null,
                "active", "person", Owner, Today.AddMonths(6)), Today, Author, Now).IsSuccess);
            DomainEvent[] facts =
            [
                DomainEventSeed.Attach(new PersonRecorded(Tenant, Owner, "Owner", null, Author, Now), Owner, 1),
                DomainEventSeed.Attach(new PersonMembershipCorrelated(Tenant, Owner, 2,
                    linked ? User : Uuid.CreateVersion4(), Author, Now), Owner, 2),
            ];
            _person = NativeFacts.Replay(new Person(Tenant, Owner), facts);
            _member = new Member(Tenant, User);
            Assert.True(_member.Register(NativeFacts.Root(_member, Uuid.CreateVersion4(), "RegisterMember", null),
                NativeFacts.Ref()).IsSuccess);
            Executor = new OwnerExecutor(Identity);
        }

        public Cntryl.Portia.RequestContext<T> Context<T>(T request, RequestInvocation? invocation = null) where T : IRequestBase =>
            new(request, new RequestDispatchContext(new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "bdgrz"),
                new Claim("sub", User.ToString())], "BdgrzSession")), invocation ??
                new HttpInvocation("POST", "/synthetic/review", "/synthetic/review", "synthetic")));

        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
            where TAggregate : Aggregate => ValueTask.FromResult(aggregate switch
            {
                ServiceIdentity when aggregate.Id == Identity.Id => (TAggregate)(Aggregate)Identity,
                Person when aggregate.Id == Owner => (TAggregate)(Aggregate)_person,
                Member when aggregate.Id == _member.Id => (TAggregate)(Aggregate)_member,
                _ => aggregate,
            });
    }

    sealed class OwnerExecutor(ServiceIdentity owner) : IAggregateExecutor
    {
        public int Calls { get; private set; }
        public Action? BeforeExecute { get; set; }

        public ValueTask<Result> ExecuteAsync<TAggregate>(TAggregate aggregate, Func<TAggregate, AggregateOutcome> operation,
            IExecutionContext context, CancellationToken ct = default) where TAggregate : Aggregate
        {
            Assert.Equal(owner.Stream, aggregate.Stream);
            Calls++;
            BeforeExecute?.Invoke();
            return ValueTask.FromResult(operation((TAggregate)(Aggregate)owner).Result);
        }

        public ValueTask<Result<TOut>> ExecuteAsync<TAggregate, TOut>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome<TOut>> operation, IExecutionContext context,
            CancellationToken ct = default) where TAggregate : Aggregate => throw new InvalidOperationException("No value response.");
    }

    sealed class Permissions(bool grant) : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(grant && permission == ServiceIdentityPermissions.Review);
    }

    sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
