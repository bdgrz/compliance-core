using System.Security.Claims;
using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class SystemInstanceRevisionHandlerTests : IAsyncDisposable
{
    readonly Uuid _tenantId = Uuid.CreateVersion4();
    readonly Uuid _applicationId = Uuid.CreateVersion4();
    readonly Uuid _systemInstanceId = Uuid.CreateVersion4();
    readonly Uuid _userId = Uuid.CreateVersion4();
    readonly DateTimeOffset _now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
    readonly ServiceProvider _provider;

    public SystemInstanceRevisionHandlerTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore, InMemoryEventStore>();
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(_now));
        services.AddPortia();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    [Fact]
    public async Task ShouldPersistInstanceRevisionAgainstItsApplicationGivenCurrentRevision()
    {
        // Arrange
        await using var scope = _provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var actor = Actor(_userId);
        var writer = services.GetRequiredService<IAggregateWriter>();
        var instance = new DeclaredSystemInstance(_tenantId, _systemInstanceId);
        Assert.True(instance.Declare(_applicationId, "Production", "production", "Boundary v1",
            "payroll-prod", Uuid.CreateVersion4(), "Original manager", _now).IsSuccess);
        await writer.SaveAsync(instance, new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
        var request = new ReviseSystemInstance(_tenantId, _applicationId, _systemInstanceId, 1,
            "Production east", "payroll-prod-east", "Boundary v2",
            "The production region moved to the east network zone.");
        var context = new RequestContext<ReviseSystemInstance>(request, actor);
        var handler = new ReviseSystemInstanceHandler(services.GetRequiredService<IAggregateExecutor>(),
            services.GetRequiredService<TimeProvider>());

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);
        var revised = await services.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new DeclaredSystemInstance(_tenantId, _systemInstanceId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(_applicationId, revised.ApplicationId);
        Assert.Equal(2, revised.Revision);
        Assert.Equal("Production east", revised.Name);
        Assert.Equal("payroll-prod-east", revised.SourceIdentifier);
        Assert.Equal("Boundary v2", revised.AccessBoundaryReference);
        Assert.Equal(_systemInstanceId, request.SystemInstanceId);
    }

    static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()),
            new Claim("email", "inventory@example.test")], "BdgrzSession"));

    public ValueTask DisposeAsync() => _provider.DisposeAsync();

    sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
