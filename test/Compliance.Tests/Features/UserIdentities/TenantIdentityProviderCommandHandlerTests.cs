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
        Assert.Equal(2, saved.Current.Revision);
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
        public DisableTenantIdentityProviderHandler Handler { get; }

        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddPortia();
            _provider = services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Handler = new DisableTenantIdentityProviderHandler(
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

        public static Cntryl.Portia.RequestContext<T> Context<T>(Uuid userId, T request)
            where T : IRequestBase => new(request, new RequestDispatchContext(Actor(userId),
                new HttpInvocation("POST", "/api/v1/tenants/test/identity-providers/test/disable",
                    "/api/v1/tenants/test/identity-providers/test/disable",
                    "identity-provider-test")));

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
