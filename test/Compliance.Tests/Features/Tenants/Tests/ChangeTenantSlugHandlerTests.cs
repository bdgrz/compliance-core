using System.Security.Claims;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Responsibilities.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.Tenants.Handlers;
using Bdgrz.Compliance.Features.Work.Authorizers;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Tenants.Tests;

public sealed class ChangeTenantSlugHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 3, 19, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldAttributeMemberGivenSlugChangeAndConfirmation()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();

        // Act
        var result = await scenario.Handler.HandleAsync(scenario.Context("acme-next"),
            CancellationToken.None);
        var requested = Assert.Single((await scenario.TenantEventsAsync())
            .Select(static record => record.Event).OfType<TenantSlugChangeRequested>());
        var replayed = await scenario.Reader.HydrateAsync(new Tenant(scenario.TenantId),
            CancellationToken.None);
        var confirmation = replayed.ConfirmSlug("acme-next");
        var changed = Assert.Single(new AggregateScenario<Tenant>(replayed).PendingEvents
            .OfType<TenantSlugChanged>());

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(confirmation.IsSuccess);
        Assert.Equal(ActorReference.ForMember(RbacIds.Member(scenario.TenantId,
            scenario.UserId), "org-admin@example.test"), requested.RequestedBy);
        Assert.Equal(Now, requested.RequestedAt);
        Assert.Equal(requested.RequestedBy, changed.RequestedBy);
        Assert.Equal(requested.RequestedAt, changed.RequestedAt);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRejectSlugGivenAnotherTenantOwnsOrRetiredIt(bool retire)
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        await scenario.SeedSlugAsync("claimed", Uuid.CreateVersion4(), retire);

        // Act
        var result = await scenario.Handler.HandleAsync(scenario.Context("claimed"),
            CancellationToken.None);
        var requests = (await scenario.TenantEventsAsync()).Select(static record => record.Event)
            .OfType<TenantSlugChangeRequested>().ToArray();

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(requests);
    }

    [Fact]
    public async Task ShouldRejectReservedSlugGivenOrgAdminRequest()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();

        // Act
        var result = await scenario.Handler.HandleAsync(scenario.Context("api"),
            CancellationToken.None);
        var requests = (await scenario.TenantEventsAsync()).Select(static record => record.Event)
            .OfType<TenantSlugChangeRequested>().ToArray();

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(requests);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("ac--me")]
    public async Task ShouldRejectMalformedSlugGivenOrgAdminRequest(string slug)
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();

        // Act
        var result = await scenario.Handler.HandleAsync(scenario.Context(slug),
            CancellationToken.None);
        var requests = (await scenario.TenantEventsAsync()).Select(static record => record.Event)
            .OfType<TenantSlugChangeRequested>().ToArray();

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(requests);
    }

    [Fact]
    public async Task ShouldReplayWithoutAppendingGivenAnExactRetryAfterTheChangeCompleted()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var commandId = Uuid.CreateVersion4();
        var first = await scenario.Handler.HandleAsync(scenario.Context("acme-next", commandId), CancellationToken.None);
        var tenant = await scenario.Reader.HydrateAsync(new Tenant(scenario.TenantId), CancellationToken.None);
        Assert.True(tenant.ConfirmSlug("acme-next").IsSuccess);
        await scenario.Writer.SaveAsync(tenant, new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        var before = (await scenario.TenantEventsAsync()).Count;

        // Act
        var retry = await scenario.Handler.HandleAsync(scenario.Context("acme-next", commandId), CancellationToken.None);
        var after = (await scenario.TenantEventsAsync()).Count;

        // Assert
        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task ShouldConflictGivenTheSameCommandIdWithAnotherSlug()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var commandId = Uuid.CreateVersion4();
        Assert.True((await scenario.Handler.HandleAsync(scenario.Context("acme-next", commandId),
            CancellationToken.None)).IsSuccess);
        var before = (await scenario.TenantEventsAsync()).Count;

        // Act
        var changed = await scenario.Handler.HandleAsync(scenario.Context("acme-other", commandId),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.Equal(before, (await scenario.TenantEventsAsync()).Count);
    }

    [Fact]
    public async Task ShouldConflictGivenAStaleExpectedRevision()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();

        // Act
        var result = await scenario.Handler.HandleAsync(scenario.Context("acme-next", expectedRevision: 1),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty((await scenario.TenantEventsAsync()).Select(static record => record.Event)
            .OfType<TenantSlugChangeRequested>());
    }

    [Fact]
    public async Task ShouldAppendNothingGivenTheCurrentSlug()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();

        // Act
        var result = await scenario.Handler.HandleAsync(scenario.Context("acme"), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty((await scenario.TenantEventsAsync()).Select(static record => record.Event)
            .OfType<TenantSlugChangeRequested>());
    }

    [Fact]
    public async Task ShouldRejectNegativeRevisionGivenANonSystemActor()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();

        // Act
        var result = await scenario.Handler.HandleAsync(scenario.Context("acme-next", expectedRevision: -1),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
    }

    sealed class Scenario : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly InMemoryEventStore _events;

        Scenario(ServiceProvider provider, AsyncServiceScope scope, InMemoryEventStore events,
            Uuid tenantId, Uuid userId)
        {
            _provider = provider;
            _scope = scope;
            _events = events;
            TenantId = tenantId;
            UserId = userId;
        }

        public Uuid TenantId { get; }
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public IAggregateReader Reader => _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        public IAggregateWriter Writer => _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        public IAggregateExecutor Executor => _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        public IImmutableContentStore Content => _scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        public ChangeTenantSlugHandler Handler => new(new NativeOperationExecutor(Reader, Executor, Content), Reader,
            Content, new FixedOperatorAccess(), new FixedTimeProvider(Now));

        public static async Task<Scenario> CreateAsync()
        {
            var events = new InMemoryEventStore();
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(events);
            services.AddSingleton<IImmutableContentStore>(new FitzKvImmutableContentStore(
                new PersistenceKvClient(new InMemoryKvClient())));
            services.AddPortia();
            var provider = services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateScopes = true });
            var scope = provider.CreateAsyncScope();
            var scenario = new Scenario(provider, scope, events, Uuid.CreateVersion4(),
                Uuid.CreateVersion4());
            var tenant = new Tenant(scenario.TenantId);
            Assert.True(tenant.Register(Uuid.CreateVersion4(), "Acme", "acme").IsSuccess);
            Assert.True(tenant.ConfirmSlug("acme").IsSuccess);
            await scenario.Writer.SaveAsync(tenant,
                new RequestDispatchContext(RequestActor.System), CancellationToken.None);
            return scenario;
        }

        public FixedRequestContext Context(string slug, Uuid commandId = default, long expectedRevision = 2) =>
            new(new ChangeTenantSlug(commandId == Uuid.Empty ? Uuid.CreateVersion4() : commandId, TenantId, slug,
                expectedRevision), Actor(UserId), Uuid.CreateVersion4());

        public async Task<List<DomainEventRecord>> TenantEventsAsync()
        {
            var records = new List<DomainEventRecord>();
            await foreach (var record in _events.ReadAsync(new EventStreamAddress("bdgrz",
                               "tenants", TenantId.ToString()), 0, CancellationToken.None))
                records.Add(record);
            return records;
        }

        public async Task SeedSlugAsync(string slug, Uuid ownerId, bool retired)
        {
            var candidate = await Reader.HydrateAsync(new TenantSlug(slug), CancellationToken.None);
            Assert.True(candidate.Register(ownerId).IsSuccess);
            if (retired)
                Assert.True(candidate.Surrender(ownerId).IsSuccess);
            await Writer.SaveAsync(candidate, new RequestDispatchContext(RequestActor.System),
                CancellationToken.None);
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class FixedRequestContext(ChangeTenantSlug request, ClaimsPrincipal actor, Uuid requestId)
        : IRequestContext<ChangeTenantSlug>
    {
        public ChangeTenantSlug Request => request;
        public ClaimsPrincipal Actor => actor;
        public Uuid ExecutionId { get; } = Uuid.CreateVersion4();
        public Uuid RequestId => requestId;
        public Uuid CorrelationId => requestId;
        public Uuid? CausationId => null;
        public Uuid CauseId => requestId;
        public DateTimeOffset StartedAt { get; } = Now;
        public RequestInvocation Invocation { get; } = new DirectInvocation();
    }

    static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()),
            new Claim("email", "org-admin@example.test")], "BdgrzSession"));

    sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
