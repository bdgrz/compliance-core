using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Portia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests;

public sealed class ComplianceCompositionTests
{
    [Fact]
    public void ShouldReturnPortiaBuilderGivenSharedApplicationComposition()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance",
            })
            .Build();

        // Act
        var application = services.AddCompliance(configuration);

        // Assert
        Assert.NotNull(application);
    }

    [Fact]
    public async Task ShouldComposeEveryTypeIBaselineSourceFamilyGivenSharedApplication()
    {
        // Arrange
        var builder = Compose();
        using var host = builder.Build();
        await using var scope = host.Services.CreateAsyncScope();

        // Act
        var contributors = scope.ServiceProvider
            .GetServices<ITypeIBaselineSourceContributor>().ToArray();
        var previewReader = scope.ServiceProvider.GetRequiredService<ITypeIBaselinePreviewReader>();

        // Assert
        Assert.Equal(TypeIBaselineSourceFamilies.Required.Count, contributors.Length);
        Assert.Equal(TypeIBaselineSourceFamilies.Required.Order(StringComparer.Ordinal),
            contributors.Select(static contributor => contributor.Family)
                .Order(StringComparer.Ordinal));
        Assert.IsType<TypeIBaselinePreviewReader>(previewReader);
    }

    [Fact]
    public async Task ShouldRejectUnprotectedRequestGivenSharedApplicationCompositionWhenPortiaValidationStarts()
    {
        // Arrange
        var builder = Compose();
        _ = builder.Services.AddPortia()
            .AddRequestHandler<UnprotectedCompositionRequestHandler>();
        RemoveFitzConnectionHostedService(builder.Services);
        using var host = builder.Build();

        // Act
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());

        // Assert
        Assert.Contains(nameof(UnprotectedCompositionRequest), failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldRejectUnprotectedRequestGivenSharedApplicationCompositionWhenPortiaDispatches()
    {
        // Arrange
        var builder = Compose();
        _ = builder.Services.AddPortia()
            .AddRequestHandler<UnprotectedCompositionRequestHandler>();
        await using var provider = builder.Services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();

        // Act
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await bus.SendAsync(new UnprotectedCompositionRequest(), RequestActor.Anonymous));

        // Assert
        Assert.Contains(nameof(UnprotectedCompositionRequest), failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldStartGivenEveryRegisteredRequestHasAuthorizationWhenPortiaValidationStarts()
    {
        // Arrange
        var builder = Compose();
        RemoveFitzConnectionHostedService(builder.Services);
        using var host = builder.Build();

        // Act
        await host.StartAsync();

        // Assert
        await host.StopAsync();
    }

    [Fact]
    public async Task ShouldAllowOnlyNamedMigrationProcessGivenProductionCompositionAuthorization()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance",
            })
            .Build();
        _ = services.AddCompliance(configuration);
        await using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var request = new MigrateFindingHistory(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4());
        var metadata = new RequestMetadata(Uuid.CreateVersion4(), Uuid.CreateVersion4(), null);

        // Act
        var named = await bus.AuthorizeAsync(request, new RequestDispatchContext(
            RequestActor.CreateSystem("migration:FindingHistoryV1", "bdgrz.system"),
            new DirectInvocation(), metadata), CancellationToken.None);
        var generic = await bus.AuthorizeAsync(request, new RequestDispatchContext(
            RequestActor.System, new DirectInvocation(), metadata), CancellationToken.None);
        var member = await bus.AuthorizeAsync(request, new RequestDispatchContext(
            RequestActor.Anonymous,
            new DirectInvocation(), metadata), CancellationToken.None);

        // Assert
        Assert.True(named.IsSuccess, named.Error?.Message);
        Assert.Equal(RequestErrorKind.Forbidden, generic.Error?.Kind);
        Assert.Equal(RequestErrorKind.Forbidden, member.Error?.Kind);
    }

    static HostApplicationBuilder Compose()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance",
        });
        _ = builder.Services.AddCompliance(builder.Configuration, developerAuthentication: true);
        return builder;
    }

    static void RemoveFitzConnectionHostedService(IServiceCollection services)
    {
        var descriptor = services.Single(candidate =>
            candidate.ServiceType == typeof(IHostedService) && candidate.ImplementationFactory is not null);
        _ = services.Remove(descriptor);
    }
}
