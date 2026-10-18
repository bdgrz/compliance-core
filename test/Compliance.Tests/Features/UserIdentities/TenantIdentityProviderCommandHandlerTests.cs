using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.UserIdentities;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.UserIdentities;

public sealed class TenantIdentityProviderCommandHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 17, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldCarryExpectedRevisionGivenProviderTestCommand()
    {
        // Arrange
        var commandType = typeof(TestTenantIdentityProvider);

        // Act
        var property = commandType.GetProperty("ExpectedRevision");

        // Assert
        Assert.NotNull(property);
        Assert.Equal(typeof(long), property.PropertyType);
    }

    [Fact]
    public async Task ShouldConfigureConnectionAsPendingTestGivenPersonalAdministrator()
    {
        // Arrange
        await using var fixture = new Fixture();
        var command = new ConfigureTenantIdentityProvider(fixture.TenantId, fixture.ProviderId,
            "https://idp.example.test", "client-app", "vault://tenant/idp/credential-v1",
            ["client-app", "api://compliance"]);

        // Act
        var result = await fixture.ConfigureHandler.HandleAsync(Fixture.Context(fixture.UserId,
            command), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TenantIdentityProvider(fixture.TenantId,
            fixture.ProviderId));
        var configured = Assert.Single((await fixture.ReadEventsAsync())
            .OfType<TenantIdentityProviderConfigured>());

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("pending_test", saved.Current!.Status);
        Assert.Equal(1, saved.Current.Revision);
        Assert.Equal(1, saved.Current.CredentialRevision);
        Assert.Equal("https://idp.example.test", saved.Current.Issuer);
        Assert.Equal("vault://tenant/idp/credential-v1",
            configured.Configuration.EncryptedCredentialReference);
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.UserId), saved.Current.ActorMemberId);
        Assert.Equal(Now, saved.Current.UpdatedAt);
    }

    [Fact]
    public async Task ShouldPersistProviderTestRequestGivenPersonalAdministrator()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedConfiguredProviderAsync();
        var command = new TestTenantIdentityProvider(fixture.TenantId, fixture.ProviderId, 1,
            "oidc-check-2026-10");

        // Act
        var result = await fixture.TestHandler.HandleAsync(Fixture.Context(fixture.UserId, command),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TenantIdentityProvider(fixture.TenantId,
            fixture.ProviderId));
        var requested = Assert.Single((await fixture.ReadEventsAsync())
            .OfType<TenantIdentityProviderTestRequested>());

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(2, saved.Revision);
        Assert.Equal("oidc-check-2026-10", saved.Current!.PendingTest!.TestKey);
        Assert.Equal(1, saved.Current.PendingTest.CredentialRevision);
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.UserId),
            saved.Current.PendingTest.RequestedByMemberId);
        Assert.Equal("oidc-check-2026-10", requested.Request.TestKey);
        Assert.Equal(Now, requested.OccurredAt);
    }

    [Fact]
    public async Task ShouldRejectStaleProviderTestRequestGivenConcurrentProviderChange()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedConfiguredProviderAsync();

        // Act
        var result = await fixture.TestHandler.HandleAsync(Fixture.Context(fixture.UserId,
            new TestTenantIdentityProvider(fixture.TenantId, fixture.ProviderId, 0, "stale-test")),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TenantIdentityProvider(fixture.TenantId,
            fixture.ProviderId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(1, saved.Revision);
        Assert.Null(saved.Current!.PendingTest);
    }

    [Fact]
    public async Task ShouldTreatRepeatedTestKeyAsIdempotentGivenSamePendingRequest()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedConfiguredProviderAsync();
        var context = Fixture.Context(fixture.UserId,
            new TestTenantIdentityProvider(fixture.TenantId, fixture.ProviderId, 1, "retry-test"));

        // Act
        var first = await fixture.TestHandler.HandleAsync(context, CancellationToken.None);
        var retry = await fixture.TestHandler.HandleAsync(context, CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TenantIdentityProvider(fixture.TenantId,
            fixture.ProviderId));
        var requested = (await fixture.ReadEventsAsync()).OfType<TenantIdentityProviderTestRequested>();

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(2, saved.Revision);
        Assert.Equal("retry-test", saved.Current!.PendingTest!.TestKey);
        Assert.Single(requested);
    }

    [Fact]
    public async Task ShouldRejectSystemActorGivenProviderTestRequest()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedConfiguredProviderAsync();
        var command = new TestTenantIdentityProvider(fixture.TenantId, fixture.ProviderId, 1,
            "system-test");
        var context = new Cntryl.Portia.RequestContext<TestTenantIdentityProvider>(command,
            new RequestDispatchContext(RequestActor.System, new HttpInvocation("POST",
                "/api/v1/tenants/test/identity-providers/test/tests",
                "/api/v1/tenants/test/identity-providers/test/tests", "identity-provider-test")));

        // Act
        var result = await fixture.TestHandler.HandleAsync(context, CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TenantIdentityProvider(fixture.TenantId,
            fixture.ProviderId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(1, saved.Revision);
        Assert.Null(saved.Current!.PendingTest);
    }

    [Fact]
    public async Task ShouldRotateCredentialAndRequireNewProviderTestGivenEnabledConnection()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedConfiguredProviderAsync();
        var current = await fixture.Reader.HydrateAsync(new TenantIdentityProvider(fixture.TenantId,
            fixture.ProviderId));
        Assert.Null(current.RequestTest(current.Revision, "test-1",
            RbacIds.Member(fixture.TenantId, fixture.UserId), "Tenant administrator",
            Now.AddMinutes(-1)));
        Assert.Null(current.RecordTest(current.Revision,
            new TenantIdentityProviderTestResultContent("test-1", "passed", null, null),
            RbacIds.Member(fixture.TenantId, fixture.UserId), "Tenant administrator",
            Now.AddMinutes(-1)));
        await fixture.Writer.SaveAsync(current, new RequestDispatchContext(RequestActor.System));

        // Act
        var result = await fixture.RotateHandler.HandleAsync(Fixture.Context(fixture.UserId,
            new RotateTenantIdentityProviderCredentials(fixture.TenantId, fixture.ProviderId,
                "vault://tenant/idp/credential-v2", "rotation-2026-10")), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TenantIdentityProvider(fixture.TenantId,
            fixture.ProviderId));
        var rotated = Assert.Single((await fixture.ReadEventsAsync())
            .OfType<TenantIdentityProviderCredentialsRotated>());

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("pending_test", saved.Current!.Status);
        Assert.Equal(4, saved.Current.Revision);
        Assert.Equal(2, saved.Current.CredentialRevision);
        Assert.Equal("vault://tenant/idp/credential-v2", rotated.EncryptedCredentialReference);
        Assert.Equal("rotation-2026-10", saved.Current.LastRotationReference);
        Assert.Single(saved.Current.TestHistory);
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.UserId), saved.Current.ActorMemberId);
    }

    [Fact]
    public async Task ShouldRejectSystemActorGivenProviderConfiguration()
    {
        // Arrange
        await using var fixture = new Fixture();
        var command = new ConfigureTenantIdentityProvider(fixture.TenantId, fixture.ProviderId,
            "https://idp.example.test", "client-app", "vault://tenant/idp/credential-v1",
            ["client-app"]);

        // Act
        var context = new Cntryl.Portia.RequestContext<ConfigureTenantIdentityProvider>(command,
            new RequestDispatchContext(RequestActor.System, new HttpInvocation("POST",
                "/api/v1/tenants/test/identity-providers", "/api/v1/tenants/test/identity-providers",
                "identity-provider-test")));
        var result = await fixture.ConfigureHandler.HandleAsync(context, CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TenantIdentityProvider(fixture.TenantId,
            fixture.ProviderId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Null(saved.Current);
        Assert.Equal(0, saved.Revision);
    }

    [Fact]
    public async Task ShouldDisableConfiguredConnectionAndRetainAttributionGivenTenantAdministrator()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedConfiguredProviderAsync();

        // Act
        var result = await fixture.Handler.HandleAsync(Fixture.Context(fixture.UserId,
            new DisableTenantIdentityProvider(fixture.TenantId, fixture.ProviderId, 1,
                "Client moved sign-in to its central identity provider.")), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TenantIdentityProvider(fixture.TenantId,
            fixture.ProviderId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("disabled", saved.Current!.Status);
        Assert.Equal(2, saved.Current.Revision);
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.UserId), saved.Current.ActorMemberId);
        Assert.Equal("Client moved sign-in to its central identity provider.",
            saved.Current.DisabledRationale);
        Assert.Equal(Now, saved.Current.UpdatedAt);
    }

    [Fact]
    public async Task ShouldRejectStaleDisableWithoutChangingConnectionGivenConcurrentUpdate()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedConfiguredProviderAsync();
        var current = await fixture.Reader.HydrateAsync(new TenantIdentityProvider(fixture.TenantId,
            fixture.ProviderId));
        Assert.Null(current.RequestTest(current.Revision, "test-1",
            RbacIds.Member(fixture.TenantId, fixture.UserId), "Tenant administrator",
            Now.AddMinutes(-1)));
        Assert.Null(current.RecordTest(current.Revision,
            new TenantIdentityProviderTestResultContent("test-1", "passed", null, null),
            RbacIds.Member(fixture.TenantId, fixture.UserId), "Tenant administrator",
            Now.AddMinutes(-1)));
        await fixture.Writer.SaveAsync(current, new RequestDispatchContext(RequestActor.System));

        // Act
        var result = await fixture.Handler.HandleAsync(Fixture.Context(fixture.UserId,
            new DisableTenantIdentityProvider(fixture.TenantId, fixture.ProviderId, 1,
                "Client no longer uses this provider.")), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TenantIdentityProvider(fixture.TenantId,
            fixture.ProviderId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal("enabled", saved.Current!.Status);
        Assert.Equal(3, saved.Current.Revision);
    }

    [Fact]
    public async Task ShouldRejectSystemActorGivenPersonalDisableCommand()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedConfiguredProviderAsync();

        // Act
        var context = new Cntryl.Portia.RequestContext<DisableTenantIdentityProvider>(
            new DisableTenantIdentityProvider(fixture.TenantId, fixture.ProviderId, 1,
                "Client no longer uses this provider."),
            new RequestDispatchContext(RequestActor.System, new HttpInvocation("POST",
                "/api/v1/tenants/test/identity-providers/test/disable",
                "/api/v1/tenants/test/identity-providers/test/disable", "identity-provider-test")));
        var result = await fixture.Handler.HandleAsync(context, CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TenantIdentityProvider(fixture.TenantId,
            fixture.ProviderId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal("pending_test", saved.Current!.Status);
        Assert.Equal(1, saved.Revision);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly TimeProvider _clock = new FixedTimeProvider(Now);

        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProviderId { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }
        public IDomainEventReader Events { get; }
        public DisableTenantIdentityProviderHandler Handler { get; }
        public ConfigureTenantIdentityProviderHandler ConfigureHandler { get; }
        public RotateTenantIdentityProviderCredentialsHandler RotateHandler { get; }
        public TestTenantIdentityProviderHandler TestHandler { get; }

        public Fixture()
        {
            var services = new ServiceCollection();
            var store = new InMemoryEventStore();
            services.AddSingleton<IEventStore>(store);
            services.AddSingleton<IDomainEventReader>(store);
            services.AddPortia();
            _provider = services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Events = _scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
            Handler = new DisableTenantIdentityProviderHandler(
                _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>(), _clock);
            ConfigureHandler = new ConfigureTenantIdentityProviderHandler(
                _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>(), _clock);
            RotateHandler = new RotateTenantIdentityProviderCredentialsHandler(
                _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>(), _clock);
            TestHandler = new TestTenantIdentityProviderHandler(
                _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>(), _clock);
        }

        public async Task SeedConfiguredProviderAsync()
        {
            var provider = new TenantIdentityProvider(TenantId, ProviderId);
            Assert.Null(provider.Configure(new TenantIdentityProviderConfigurationContent(
                "https://idp.example.test", "client-app", "vault://tenant/idp/credential-v1",
                ["client-app", "api://compliance"]), RbacIds.Member(TenantId, UserId),
                "Tenant administrator", Now.AddMinutes(-2)));
            await Writer.SaveAsync(provider, new RequestDispatchContext(RequestActor.System));
        }

        public async Task<IReadOnlyList<DomainEvent>> ReadEventsAsync()
        {
            var aggregate = new TenantIdentityProvider(TenantId, ProviderId);
            var events = new List<DomainEvent>();
            await foreach (var record in Events.ReadAsync(aggregate.Stream, 0,
                               CancellationToken.None))
                events.Add(record.Event);
            return events;
        }

        public static Cntryl.Portia.RequestContext<T> Context<T>(Uuid userId, T request)
            where T : IRequestBase
        {
            var path = request switch
            {
                ConfigureTenantIdentityProvider => "/api/v1/tenants/test/identity-providers/test",
                TestTenantIdentityProvider =>
                    "/api/v1/tenants/test/identity-providers/test/tests",
                RotateTenantIdentityProviderCredentials =>
                    "/api/v1/tenants/test/identity-providers/test/credential-rotations",
                _ => "/api/v1/tenants/test/identity-providers/test/disable",
            };
            return new(request, new RequestDispatchContext(Actor(userId),
                new HttpInvocation("POST", path, path, "identity-provider-test")));
        }

        static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()),
                new Claim("email", "administrator@example.test")], "BdgrzSession"));

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
