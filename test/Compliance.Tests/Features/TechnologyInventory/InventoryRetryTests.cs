using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Commands;
using Bdgrz.Compliance.Features.TechnologyInventory.Handlers;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.TechnologyInventory;
using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.TechnologyInventory;

/// <summary>
///     Component, information asset and data flow operations identified by their own command identity (ROOTTECH-02):
///     an exact retry replays the original answer however far the record moved, the same identity with different
///     intent conflicts, and a flow carries an unbounded complete asset list (ROOTTECH-05).
/// </summary>
public sealed class InventoryRetryTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldReplayOriginalRegistrationGivenExactComponentRetryAfterLaterRevision()
    {
        // Arrange
        await using var host = await Host.CreateAsync();
        var id = Uuid.CreateVersion4();
        var create = new RecordTechnologyComponent(id, host.TenantId, "repository", "api", host.OwnerId);
        var first = await host.RecordComponentAsync(create);
        var revise = new ReviseTechnologyComponent(Uuid.CreateVersion4(), host.TenantId, id, 1, "api v2", host.OwnerId,
            "active");
        var revised = await host.ReviseComponentAsync(revise);

        // Act
        var createRetry = await host.RecordComponentAsync(create);
        var reviseRetry = await host.ReviseComponentAsync(revise);
        var changedCreate = await host.RecordComponentAsync(create with { Name = "different" });
        var changedRevise = await host.ReviseComponentAsync(revise with { Name = "different" });
        var another = await host.ReviseComponentAsync(revise with { CommandId = Uuid.CreateVersion4() });
        var owner = await host.Reader.HydrateAsync(new TechnologyComponent(host.TenantId, id));

        // Assert
        Assert.Equal(first.Value, createRetry.Value);
        Assert.True(revised.IsSuccess);
        Assert.True(reviseRetry.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changedCreate.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changedRevise.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, another.Error!.Kind);
        Assert.Equal(2, owner.Revision);
        Assert.Equal("api v2", owner.Content!.Name);
    }

    [Fact]
    public async Task ShouldReplayOriginalRegistrationGivenExactAssetRetryAfterLaterRevision()
    {
        // Arrange
        await using var host = await Host.CreateAsync();
        var id = Uuid.CreateVersion4();
        var create = new RecordInformationAsset(id, host.TenantId, "Customer PII", "restricted", "RET-1", host.OwnerId);
        var first = await host.RecordAssetAsync(create);
        var revise = new ReviseInformationAsset(Uuid.CreateVersion4(), host.TenantId, id, 1, "Customer PII",
            "confidential", "RET-1", host.OwnerId, "active");
        Assert.True((await host.ReviseAssetAsync(revise)).IsSuccess);

        // Act
        var createRetry = await host.RecordAssetAsync(create);
        var reviseRetry = await host.ReviseAssetAsync(revise);
        var changedCreate = await host.RecordAssetAsync(create with { Classification = "public" });
        var stale = await host.ReviseAssetAsync(revise with { CommandId = Uuid.CreateVersion4() });

        // Assert
        Assert.Equal(first.Value, createRetry.Value);
        Assert.True(reviseRetry.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changedCreate.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error!.Kind);
    }

    [Fact]
    public async Task ShouldCarryUnboundedDistinctAssetsAndRefuseEmptyGivenDataFlow()
    {
        // Arrange
        await using var host = await Host.CreateAsync();
        var source = Uuid.CreateVersion4();
        Assert.True((await host.RecordComponentAsync(new RecordTechnologyComponent(source, host.TenantId, "repository",
            "api", host.OwnerId))).IsSuccess);
        var assets = new List<Uuid>();
        for (var index = 0; index < 150; index++)
        {
            var asset = Uuid.CreateVersion4();
            assets.Add(asset);
            Assert.True((await host.RecordAssetAsync(new RecordInformationAsset(asset, host.TenantId, $"Asset {index}",
                "internal", "RET-1", host.OwnerId))).IsSuccess);
        }

        var flow = Uuid.CreateVersion4();
        var build = Flow(flow);
        RecordDataFlow Flow(Uuid id) => new(id, host.TenantId, "technology_component", source, "external_party", assets,
            "Nightly export", true, true, new DateOnly(2026, 1, 1), host.OwnerId, null, "Payroll Co");

        // Act
        var recorded = await host.RecordFlowAsync(build);
        var retry = await host.RecordFlowAsync(build);
        var empty = await host.RecordFlowAsync(Flow(Uuid.CreateVersion4()) with { InformationAssetIds = [] });
        var duplicate = await host.RecordFlowAsync(Flow(Uuid.CreateVersion4()) with
        {
            InformationAssetIds = [assets[0], assets[0]],
        });
        var owner = await host.Reader.HydrateAsync(new DataFlow(host.TenantId, flow));

        // Assert
        Assert.True(recorded.IsSuccess, recorded.Error?.Message);
        Assert.Equal(recorded.Value, retry.Value);
        Assert.Equal(150, owner.Content!.InformationAssetIds.Count);
        Assert.Equal(RequestErrorKind.Validation, empty.Error!.Kind);
        Assert.Equal(RequestErrorKind.Validation, duplicate.Error!.Kind);
    }

    [Fact]
    public async Task ShouldReplayOriginalAnswerGivenExactFlowRevisionRetry()
    {
        // Arrange
        await using var host = await Host.CreateAsync();
        var source = Uuid.CreateVersion4();
        Assert.True((await host.RecordComponentAsync(new RecordTechnologyComponent(source, host.TenantId, "repository",
            "api", host.OwnerId))).IsSuccess);
        var asset = Uuid.CreateVersion4();
        Assert.True((await host.RecordAssetAsync(new RecordInformationAsset(asset, host.TenantId, "Asset", "internal",
            "RET-1", host.OwnerId))).IsSuccess);
        var flow = Uuid.CreateVersion4();
        Assert.True((await host.RecordFlowAsync(new RecordDataFlow(flow, host.TenantId, "technology_component", source,
            "external_party", [asset], "Export", true, true, new DateOnly(2026, 1, 1), host.OwnerId, null,
            "Payroll Co"))).IsSuccess);
        var revise = new ReviseDataFlow(Uuid.CreateVersion4(), host.TenantId, flow, 1, "technology_component", source,
            "external_party", [asset], "Export v2", true, true, new DateOnly(2026, 2, 1), host.OwnerId, "active", null,
            "Payroll Co");
        Assert.True((await host.ReviseFlowAsync(revise)).IsSuccess);

        // Act
        var retry = await host.ReviseFlowAsync(revise);
        var changed = await host.ReviseFlowAsync(revise with { Purpose = "Other" });

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error!.Kind);
        Assert.Equal(2, (await host.Reader.HydrateAsync(new DataFlow(host.TenantId, flow))).Revision);
    }

    sealed class Host : IAsyncDisposable
    {
        readonly ServiceProvider _provider;

        Host(ServiceProvider provider) => _provider = provider;

        public Uuid TenantId { get; } = Uuid.CreateVersion4();

        public Uuid OwnerId { get; } = Uuid.CreateVersion4();

        public IAggregateReader Reader => _provider.GetRequiredService<IAggregateReader>();

        static readonly System.Security.Claims.ClaimsPrincipal Author =
            ProgramManagementServices.Actor(Uuid.CreateVersion4());

        public static async Task<Host> CreateAsync()
        {
            var services = new ServiceCollection();
            var events = new InMemoryEventStore();
            services.AddSingleton<IEventStore>(events);
            services.AddSingleton<IDomainEventReader>(events);
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<IImmutableContentStore>(new FitzKvImmutableContentStore(
                new PersistenceKvClient(new InMemoryKvClient())));
            services.AddPortia();
            services.AddPersonNativeFixtures();
            var host = new Host(services.BuildServiceProvider());
            var person = new PersonRules(host.TenantId, host.OwnerId);
            var result = person.Record("Owner", null, ActorReference.ForMember(Uuid.CreateVersion4(), "Seed"), Now);
            Assert.True(result.IsSuccess, result.Error?.Message);
            await PersonSeeds.AppendAsync(host._provider, person);
            return host;
        }

        T Get<T>() where T : notnull => _provider.GetRequiredService<T>();

        TechnologyInventoryReferences References() => new(Reader, null!, null!, Get<IImmutableContentStore>());

        public ValueTask<Result<TechnologyComponentRegistration>> RecordComponentAsync(RecordTechnologyComponent request) =>
            new RecordTechnologyComponentHandler(NewOperations(), Reader, References(),
                Get<IImmutableContentStore>(), TimeProvider.System).HandleAsync(
                new RequestContext<RecordTechnologyComponent>(request, Author), CancellationToken.None);

        public ValueTask<Result> ReviseComponentAsync(ReviseTechnologyComponent request) =>
            new ReviseTechnologyComponentHandler(NewOperations(), Reader, References(),
                Get<IImmutableContentStore>(), TimeProvider.System).HandleAsync(
                new RequestContext<ReviseTechnologyComponent>(request, Author), CancellationToken.None);

        public ValueTask<Result<InformationAssetRegistration>> RecordAssetAsync(RecordInformationAsset request) =>
            new RecordInformationAssetHandler(NewOperations(), Reader, References(),
                Get<IImmutableContentStore>(), TimeProvider.System).HandleAsync(
                new RequestContext<RecordInformationAsset>(request, Author), CancellationToken.None);

        public ValueTask<Result> ReviseAssetAsync(ReviseInformationAsset request) =>
            new ReviseInformationAssetHandler(NewOperations(), Reader, References(),
                Get<IImmutableContentStore>(), TimeProvider.System).HandleAsync(
                new RequestContext<ReviseInformationAsset>(request, Author), CancellationToken.None);

        public ValueTask<Result<DataFlowRegistration>> RecordFlowAsync(RecordDataFlow request) =>
            new RecordDataFlowHandler(NewOperations(), Reader, References(),
                Get<IImmutableContentStore>(), TimeProvider.System).HandleAsync(
                new RequestContext<RecordDataFlow>(request, Author), CancellationToken.None);

        public ValueTask<Result> ReviseFlowAsync(ReviseDataFlow request) =>
            new ReviseDataFlowHandler(NewOperations(), Reader, References(),
                Get<IImmutableContentStore>(), TimeProvider.System).HandleAsync(
                new RequestContext<ReviseDataFlow>(request, Author), CancellationToken.None);

        NativeOperationExecutor NewOperations() => new(Reader, Get<IAggregateExecutor>(), Get<IImmutableContentStore>());

        public ValueTask DisposeAsync() => _provider.DisposeAsync();
    }
}
