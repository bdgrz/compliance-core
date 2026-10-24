using System.Security.Claims;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.TechnologyInventory;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.TechnologyInventory;

public sealed class InventoryRegisterReadConsistencyTests
{
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Recorder");
    static readonly Uuid Owner = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, RequestErrorKind.Validation)]
    [InlineData(1, RequestErrorKind.Conflict)]
    [InlineData(2, RequestErrorKind.Conflict)]
    public async Task ShouldDistinguishInvalidRevisionAndLagGivenRecordedUnprojectedLocation(
        long minimum, RequestErrorKind expected)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var consistency = Consistency(new FitzInventoryRegisterDirectory(new InMemoryKvClient()),
            NamedRecordFacts.RecordedLocation(tenant, id), new InMemoryEventStore());

        // Act
        var result = await consistency.GetLocationAsync(tenant, id, minimum, CancellationToken.None);

        // Assert
        Assert.Equal(expected, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Fact]
    public async Task ShouldReturnNotFoundGivenUnknownOrForeignRecord()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var directory = new FitzInventoryRegisterDirectory(new InMemoryKvClient());
        await Project(directory, tenant, NamedRecordFacts.LocationFact(tenant, id, 1), Loc("HQ"));
        var otherTenant = Uuid.CreateVersion4();
        var foreign = Consistency(directory, new Location(otherTenant, id), new InMemoryEventStore());
        var unknown = Consistency(directory, new Location(tenant, Uuid.CreateVersion4()), new InMemoryEventStore());

        // Act
        var leak = await foreign.GetLocationAsync(otherTenant, id, null, CancellationToken.None);
        var missing = await unknown.GetLocationAsync(tenant, Uuid.CreateVersion4(), null, CancellationToken.None);
        var processLeak = await Consistency(directory, new Location(otherTenant, id),
            new InMemoryEventStore(), new OperationalProcess(otherTenant, id))
            .GetOperationalProcessAsync(otherTenant, id, null, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(leak.Error).Kind);
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(missing.Error).Kind);
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(processLeak.Error).Kind);
    }

    [Fact]
    public async Task ShouldServeCurrentRevisionOnlyOnceProjectionReachesSourceGivenLaggingProjection()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var source = NamedRecordFacts.RecordedLocation(tenant, id, 2);
        var directory = new FitzInventoryRegisterDirectory(new InMemoryKvClient());
        await Project(directory, tenant, NamedRecordFacts.LocationFact(tenant, id, 1, Author, Now), Loc("HQ"));
        var consistency = Consistency(directory, source, new InMemoryEventStore());

        // Act
        var lagging = await new GetLocationHandler(consistency).HandleAsync(
            new RequestContext<GetLocation>(new GetLocation(tenant, id), new ClaimsPrincipal()), CancellationToken.None);
        var history = await new ListLocationRevisionsHandler(directory, consistency).HandleAsync(
            new RequestContext<ListLocationRevisions>(new ListLocationRevisions(tenant, id), new ClaimsPrincipal()),
            CancellationToken.None);
        await Project(directory, tenant, NamedRecordFacts.LocationFact(tenant, id, 2, Author, Now.AddMinutes(1)),
            Loc("Main"));
        var caughtUp = await new GetLocationHandler(consistency).HandleAsync(
            new RequestContext<GetLocation>(new GetLocation(tenant, id, 2), new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(lagging.Error).Kind);
        Assert.True(Assert.IsType<RequestError>(lagging.Error).IsTransient);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(history.Error).Kind);
        Assert.Equal("Main", caughtUp.Value.Content.Name);
    }

    [Theory]
    [InlineData(0, RequestErrorKind.Validation)]
    [InlineData(201, RequestErrorKind.Validation)]
    [InlineData(1, RequestErrorKind.Conflict)]
    public async Task ShouldRejectInvalidLimitsOrEmptyStalePageGivenUnprojectedSource(int limit,
        RequestErrorKind expected)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var events = new InMemoryEventStore();
        await events.AppendAsync(new Location(tenant, id).Stream, 0,
            [DomainEventSeed.Attach(NamedRecordFacts.LocationFact(tenant, id, 1, Author, Now), id, 1)]);
        var directory = new FitzInventoryRegisterDirectory(new InMemoryKvClient());
        var consistency = Consistency(directory, NamedRecordFacts.RecordedLocation(tenant, id), events);

        // Act
        var locations = await new ListLocationsHandler(directory, consistency).HandleAsync(
            new RequestContext<ListLocations>(new ListLocations(tenant, limit), new ClaimsPrincipal()), CancellationToken.None);
        var processes = await new ListOperationalProcessesHandler(directory, consistency).HandleAsync(
            new RequestContext<ListOperationalProcesses>(new ListOperationalProcesses(tenant, limit), new ClaimsPrincipal()),
            CancellationToken.None);

        // Assert
        Assert.Equal(expected, Assert.IsType<RequestError>(locations.Error).Kind);
        Assert.Equal(expected, Assert.IsType<RequestError>(processes.Error).Kind);
    }

    [Fact]
    public async Task ShouldTranslateForeignCursorsAndIsolateTenantsGivenCaughtUpProjection()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var other = Uuid.CreateVersion4();
        var directory = new FitzInventoryRegisterDirectory(new InMemoryKvClient());
        await Project(directory, tenant, NamedRecordFacts.ProcessFact(tenant, Uuid.CreateVersion4(), 1, Author, Now),
            Proc("One"));
        await Project(directory, tenant, NamedRecordFacts.ProcessFact(tenant, Uuid.CreateVersion4(), 1, Author, Now),
            Proc("Two"));
        var consistency = Consistency(directory, new Location(tenant, Uuid.CreateVersion4()), new InMemoryEventStore());
        var handler = new ListOperationalProcessesHandler(directory, consistency);
        var first = await handler.HandleAsync(new RequestContext<ListOperationalProcesses>(
            new ListOperationalProcesses(tenant, 1), new ClaimsPrincipal()), CancellationToken.None);

        // Act
        var foreign = await handler.HandleAsync(new RequestContext<ListOperationalProcesses>(
            new ListOperationalProcesses(other, 1, first.Value.NextCursor), new ClaimsPrincipal()), CancellationToken.None);
        var otherTenant = await handler.HandleAsync(new RequestContext<ListOperationalProcesses>(
            new ListOperationalProcesses(other), new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        Assert.True(first.IsSuccess);
        Assert.Single(first.Value.Items);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(foreign.Error).Kind);
        Assert.True(otherTenant.IsSuccess);
        Assert.Empty(otherTenant.Value.Items);
    }

    [Fact]
    public async Task ShouldReportActiveOnlyGivenRetiredOrForeignRegisterRecordsAsBoundarySubjects()
    {
        // Arrange
        var collection = new ServiceCollection();
        var events = new InMemoryEventStore();
        collection.AddSingleton<IEventStore>(events);
        collection.AddSingleton<IDomainEventReader>(events);
        collection.AddSingleton<IImmutableContentStore>(new FitzKvImmutableContentStore(
            new PersistenceKvClient(new InMemoryKvClient())));
        collection.AddPortia();
        await using var services = collection.BuildServiceProvider();
        var tenant = Uuid.CreateVersion4();
        var locationId = Uuid.CreateVersion4();
        var processId = Uuid.CreateVersion4();
        await NamedRecordFacts.SeedLocationAsync(services, tenant, locationId, Loc("HQ"));
        await NamedRecordFacts.SeedProcessAsync(services, tenant, processId, Proc("Backups"));
        var references = new TechnologyInventoryReferences(services.GetRequiredService<IAggregateReader>(), null!,
            null!, services.GetRequiredService<IImmutableContentStore>());

        // Act
        var activeLocation = await references.IsActiveAsync(tenant, "location", locationId);
        var activeProcess = await references.IsActiveAsync(tenant, "process", processId);
        var wrongKind = await references.IsActiveAsync(tenant, "process", locationId);
        await NamedRecordFacts.SeedLocationAsync(services, tenant, locationId, Loc("HQ") with { Lifecycle = "retired" });
        var retired = await references.IsActiveAsync(tenant, "location", locationId);

        // Assert
        Assert.True(activeLocation);
        Assert.True(activeProcess);
        Assert.False(wrongKind);
        Assert.False(retired);
    }

    static async Task Project(FitzInventoryRegisterDirectory directory, Uuid tenant, LocationRevisionRecorded ev,
        LocationContent content)
    {
        await using var batch = await directory.BeginAsync(Batch(directory, tenant));
        await directory.ApplyAsync(ev, content);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }

    static async Task Project(FitzInventoryRegisterDirectory directory, Uuid tenant,
        OperationalProcessRevisionRecorded ev, OperationalProcessContent content)
    {
        await using var batch = await directory.BeginAsync(Batch(directory, tenant));
        await directory.ApplyAsync(ev, content);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }

    static ProjectionBatchContext Batch(FitzInventoryRegisterDirectory directory, Uuid tenant) => new(
        new CheckpointIdentity(FitzInventoryRegisterDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenant.ToString(), "inventory-registers")), ProjectionCheckpoint.Start);

    static InventoryRegisterReadConsistency Consistency(FitzInventoryRegisterDirectory directory,
        Location locations, IDomainEventReader events, OperationalProcess? processes = null) =>
        new(directory, new SourceReader(locations, processes), events);

    static LocationContent Loc(string name) => new("physical_site", name, null, Owner, "active");

    static OperationalProcessContent Proc(string name) =>
        new(name, "Purpose", Owner, null, null, "active");

    sealed class SourceReader(Location locations, OperationalProcess? processes) : IAggregateReader
    {
        public ValueTask<T> HydrateAsync<T>(T aggregate, CancellationToken ct = default)
            where T : Aggregate =>
            ValueTask.FromResult(aggregate switch
            {
                Location => (T)(Aggregate)locations,
                OperationalProcess => (T)(Aggregate)(processes ?? new OperationalProcess(Uuid.CreateVersion4(),
                    Uuid.CreateVersion4())),
                _ => aggregate,
            });
    }
}
