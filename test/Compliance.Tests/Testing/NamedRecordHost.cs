using System.Security.Claims;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NamedRecords;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.TechnologyInventory;
using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Composes the named-record name-change flow end to end over in-memory Fitz and an in-memory event store: the six
///     public admission handlers, the twelve receivers on the real request bus, the process kernel and the test
///     source-fact resolver. A test admits a change, then drives its process when it wants the record to move, which is
///     how a paused or crashed step is exercised.
/// </summary>
sealed class NamedRecordHost : IAsyncDisposable
{
    public NamedRecordHost()
    {
        var services = new ServiceCollection();
        Events = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(Events);
        services.AddSingleton<IDomainEventReader>(Events);
        services.AddSingleton<IProjectionCheckpointStore, InMemoryProjectionCheckpointStore>();
        services.AddSingleton(TimeProvider.System);
        var kv = new InMemoryKvClient();
        services.AddSingleton<IImmutableContentStore>(new FitzKvImmutableContentStore(new PersistenceKvClient(kv)));
        services.AddSingleton<IProcessRecordStore>(new FitzKvProcessRecordStore(new PersistenceKvClient(kv)));
        services.AddSingleton(ProviderNameChangeProcess.Definition);
        services.AddSingleton(LocationNameChangeProcess.Definition);
        services.AddSingleton(OperationalProcessNameChangeProcess.Definition);
        services.AddSingleton<ProcessCatalog>(provider => new ProcessCatalog(provider.GetServices<ProcessDefinition>()));
        services.AddSingleton<ISourceFactResolver>(provider =>
            new EventStoreSourceFactResolver(provider.GetRequiredService<IDomainEventReader>()));
        services.AddScoped<NativeOperationExecutor>();
        services.AddScoped<ProcessDriver>();
        services.AddScoped<ProcessRootReaction>();
        services.AddScoped<ReceiverAppend>();
        services.AddScoped<NamedRecordReceivers>();
        services.AddScoped<NamedRecordReactions>();
        services.AddScoped<NamedRecordAdmission>();
        services.AddScoped<ProviderReferences>(provider => new ProviderReferences(
            provider.GetRequiredService<IAggregateReader>(), Events));
        services.AddScoped(provider => new TechnologyInventoryReferences(
            provider.GetRequiredService<IAggregateReader>(), null!, null!,
            provider.GetRequiredService<IImmutableContentStore>()));
        services.AddPortia()
            .AddRequestHandler<ClaimProviderNameHandler>().AddRequestHandler<ApplyProviderChangeHandler>()
            .AddRequestHandler<ReleaseProviderNameHandler>().AddRequestHandler<CancelProviderChangeHandler>()
            .AddRequestHandler<ClaimLocationNameHandler>().AddRequestHandler<ApplyLocationChangeHandler>()
            .AddRequestHandler<ReleaseLocationNameHandler>().AddRequestHandler<CancelLocationChangeHandler>()
            .AddRequestHandler<ClaimOperationalProcessNameHandler>()
            .AddRequestHandler<ApplyOperationalProcessChangeHandler>()
            .AddRequestHandler<ReleaseOperationalProcessNameHandler>()
            .AddRequestHandler<CancelOperationalProcessChangeHandler>()
            .AddRequestAuthorizer<NamedRecordReceiverAuthorizer>();
        Provider = services.BuildServiceProvider();
        Content = Provider.GetRequiredService<IImmutableContentStore>();
        Store = Provider.GetRequiredService<IProcessRecordStore>();
    }

    public ServiceProvider Provider { get; }

    public InMemoryEventStore Events { get; }

    public IImmutableContentStore Content { get; }

    public IProcessRecordStore Store { get; }

    public Uuid TenantId { get; } = Uuid.CreateVersion4();

    public Uuid UserId { get; } = Uuid.CreateVersion4();

    /// <summary>A workforce person every location and process can name as their owner.</summary>
    public Uuid OwnerPersonId { get; } = Uuid.CreateVersion4();

    public ClaimsPrincipal Author => ProgramManagementServices.Actor(UserId);

    public async Task SeedOwnerAsync()
    {
        var executor = Provider.GetRequiredService<IAggregateExecutor>();
        var result = await executor.ExecuteAsync(new Person(TenantId, OwnerPersonId),
            person => AggregateOutcome.CommitOnSuccess(person.Record("Owner", null,
                ActorReference.ForMember(Uuid.CreateVersion4(), "Seed"), DateTimeOffset.UtcNow)),
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    // --- admission

    public async Task<Result<OperationAcceptedResponse>> RecordProviderAsync(string name, Uuid? id = null,
        ClaimsPrincipal? actor = null)
    {
        await using var scope = Provider.CreateAsyncScope();
        var request = new RecordProvider(id ?? Uuid.CreateVersion4(), TenantId, new ProviderContent(name, "supplier"));
        return await new RecordProviderHandler(scope.ServiceProvider.GetRequiredService<NamedRecordAdmission>(),
            scope.ServiceProvider.GetRequiredService<ProviderReferences>()).HandleAsync(
            new RequestContext<RecordProvider>(request, actor ?? Author), CancellationToken.None);
    }

    public async Task<Result<OperationAcceptedResponse>> ReviseProviderAsync(Uuid providerId, string name,
        long expectedRevision, Uuid? commandId = null, ClaimsPrincipal? actor = null)
    {
        await using var scope = Provider.CreateAsyncScope();
        var request = new ReviseProvider(commandId ?? Uuid.CreateVersion4(), TenantId, providerId, expectedRevision,
            new ProviderContent(name, "supplier"));
        return await new ReviseProviderHandler(scope.ServiceProvider.GetRequiredService<NamedRecordAdmission>(),
            scope.ServiceProvider.GetRequiredService<ProviderReferences>()).HandleAsync(
            new RequestContext<ReviseProvider>(request, actor ?? Author), CancellationToken.None);
    }

    public async Task<Result<OperationAcceptedResponse>> RecordLocationAsync(string name, string kind = "physical_site",
        Uuid? id = null)
    {
        await using var scope = Provider.CreateAsyncScope();
        var request = new RecordLocation(id ?? Uuid.CreateVersion4(), TenantId, kind, name, OwnerPersonId);
        return await new RecordLocationHandler(scope.ServiceProvider.GetRequiredService<NamedRecordAdmission>(),
            scope.ServiceProvider.GetRequiredService<TechnologyInventoryReferences>()).HandleAsync(
            new RequestContext<RecordLocation>(request, Author), CancellationToken.None);
    }

    public async Task<Result<OperationAcceptedResponse>> ReviseLocationAsync(Uuid locationId, string name,
        long expectedRevision, string lifecycle = "active", Uuid? commandId = null)
    {
        await using var scope = Provider.CreateAsyncScope();
        var request = new ReviseLocation(commandId ?? Uuid.CreateVersion4(), TenantId, locationId, expectedRevision,
            name, OwnerPersonId, lifecycle);
        return await new ReviseLocationHandler(scope.ServiceProvider.GetRequiredService<NamedRecordAdmission>(),
            scope.ServiceProvider.GetRequiredService<TechnologyInventoryReferences>(),
            scope.ServiceProvider.GetRequiredService<IAggregateReader>(), Content).HandleAsync(
            new RequestContext<ReviseLocation>(request, Author), CancellationToken.None);
    }

    public async Task<Result<OperationAcceptedResponse>> RecordProcessAsync(string name, Uuid? id = null)
    {
        await using var scope = Provider.CreateAsyncScope();
        var request = new RecordOperationalProcess(id ?? Uuid.CreateVersion4(), TenantId, name, "Purpose",
            OwnerPersonId);
        return await new RecordOperationalProcessHandler(scope.ServiceProvider.GetRequiredService<NamedRecordAdmission>(),
            scope.ServiceProvider.GetRequiredService<TechnologyInventoryReferences>()).HandleAsync(
            new RequestContext<RecordOperationalProcess>(request, Author), CancellationToken.None);
    }

    public async Task<Result<OperationAcceptedResponse>> ReviseProcessAsync(Uuid processId, string name,
        long expectedRevision, string lifecycle = "active", Uuid? commandId = null)
    {
        await using var scope = Provider.CreateAsyncScope();
        var request = new ReviseOperationalProcess(commandId ?? Uuid.CreateVersion4(), TenantId, processId,
            expectedRevision, name, "Purpose", OwnerPersonId, lifecycle);
        return await new ReviseOperationalProcessHandler(scope.ServiceProvider.GetRequiredService<NamedRecordAdmission>(),
            scope.ServiceProvider.GetRequiredService<TechnologyInventoryReferences>()).HandleAsync(
            new RequestContext<ReviseOperationalProcess>(request, Author), CancellationToken.None);
    }

    // --- the process

    /// <summary>Starts and drives the process of one admitted change, exactly as its root-fact reactor would.</summary>
    public async Task<ProcessRecord> DriveAsync(NamedRecordKindDescriptor kind, Uuid operationId)
    {
        var record = await AcceptedRecordAsync(kind, operationId);
        await using var scope = Provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<NamedRecordReactions>().StartAsync(kind,
            new RecordReactorContext(record), (INamedRecordFact)record.Event, "test", CancellationToken.None);
        return (await Store.ReadAsync(KeyOf(kind, operationId)))!;
    }

    /// <summary>Creates the process record of a change without driving it, as the reactor does before its first dispatch.</summary>
    public async Task<ProcessRecord> PrepareProcessAsync(NamedRecordKindDescriptor kind, Uuid operationId)
    {
        var record = await AcceptedRecordAsync(kind, operationId);
        var fact = (INamedRecordFact)record.Event;
        await using var scope = Provider.CreateAsyncScope();
        var targets = await scope.ServiceProvider.GetRequiredService<NamedRecordReactions>()
            .FreezeTargetsAsync(kind, TenantId.ToString(), fact);
        return await Store.CreateIfAbsentAsync(ProcessRecord.Start(KeyOf(kind, operationId),
            NativeFactReference.Of(record.Stream, record.Event), fact.Operation.Episode, fact.Intent, targets,
            DateTimeOffset.UtcNow));
    }

    /// <summary>Resumes an existing process record, as the sweep would.</summary>
    public async Task<ProcessRecord> ResumeAsync(NamedRecordKindDescriptor kind, Uuid operationId,
        string worker = "sweep")
    {
        await using var scope = Provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ProcessDriver>().RunAsync(KeyOf(kind, operationId),
            worker, new RequestDispatchContext(RequestActor.System), CancellationToken.None);
    }

    /// <summary>Sends one receiver command through the real bus as the system principal.</summary>
    public async Task<Result<ReceiverOutcome>> SendAsync(IRequest<ReceiverOutcome> command,
        ClaimsPrincipal? actor = null)
    {
        await using var scope = Provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(command,
            new RequestDispatchContext(actor ?? RequestActor.System), CancellationToken.None);
    }

    /// <summary>Builds the exact receiver command the driver would send for a step of the record.</summary>
    public async Task<IRequest<ReceiverOutcome>> CommandAsync(NamedRecordKindDescriptor kind, ProcessRecord record,
        string receiver, string? target = null)
    {
        var definition = Provider.GetRequiredService<ProcessCatalog>().Get(kind.ProcessName);
        var stage = definition.Abort.Contains(receiver)
            ? "abort"
            : definition.Stages.Single(item => item.Receivers.Contains(receiver)).Stage;
        var step = new ProcessStep(record, stage, receiver, target, Bdgrz.Compliance.Features.Kernel.EffectIdentity
            .CommandId(record.Source.EventId, $"{record.Key.ProcessKind}:{receiver}", target ?? "-", record.Episode));
        return await definition.CreateReceiver(step, Provider, CancellationToken.None)
            ?? throw new InvalidOperationException($"{receiver} is not selected for this record.");
    }

    public ProcessKey KeyOf(NamedRecordKindDescriptor kind, Uuid operationId) =>
        new(kind.ProcessName, TenantId.ToString(), operationId);

    public Task<ProcessRecord?> ProcessAsync(NamedRecordKindDescriptor kind, Uuid operationId) =>
        Store.ReadAsync(KeyOf(kind, operationId));

    /// <summary>The accepted-change fact of an operation, as the reactor would see it.</summary>
    public async Task<DomainEventRecord> AcceptedRecordAsync(NamedRecordKindDescriptor kind, Uuid operationId)
    {
        await foreach (var record in Events.ReadAsync(EventStreamPattern.ForPattern(TenantId.ToString()), EventCursor.Start, CancellationToken.None))
        {
            if (record.Event is INamedRecordFact { Operation: { } operation } fact &&
                operation.LogicalId == operationId && operation.Owner == fact.Owner &&
                operation.CommandKind == (fact.Revision == 0 ? kind.CreateCommandKind : kind.ReviseCommandKind))
                return record;
        }

        throw new InvalidOperationException($"No accepted change carries operation {operationId}.");
    }

    // --- reads

    public async Task<List<DomainEvent>> FactsAsync(EventStreamAddress stream)
    {
        var facts = new List<DomainEvent>();
        try
        {
            await foreach (var record in Events.ReadAsync(stream, 0))
                facts.Add(record.Event);
        }
        catch (InvalidOperationException)
        {
            // An unknown stream reads as empty.
        }

        return facts;
    }

    public async Task<T> HydrateAsync<T>(T aggregate) where T : Aggregate =>
        await Provider.GetRequiredService<IAggregateReader>().HydrateAsync(aggregate);

    public Task<Provider> ProviderAsync(Uuid id) => HydrateAsync(new Provider(TenantId, id));

    public Task<Location> LocationAsync(Uuid id) => HydrateAsync(new Location(TenantId, id));

    public Task<OperationalProcess> ProcessOwnerAsync(Uuid id) => HydrateAsync(new OperationalProcess(TenantId, id));

    /// <summary>The claim owner of a normalized provider name.</summary>
    public Task<ProviderNameClaim> ProviderClaimAsync(string name) =>
        HydrateAsync(new ProviderNameClaim(TenantId, NameNormalization.Normalize(name)));

    public Task<LocationNameClaim> LocationClaimAsync(string name, string kind = "physical_site") =>
        HydrateAsync(new LocationNameClaim(TenantId, LocationNames.Descriptor.ClaimStream(TenantId, kind,
            NameNormalization.Normalize(name))));

    public Task<OperationalProcessNameClaim> ProcessClaimAsync(string name) =>
        HydrateAsync(new OperationalProcessNameClaim(TenantId, OperationalProcessNames.Descriptor.ClaimStream(
            TenantId, OperationalProcessNames.NameKind, NameNormalization.Normalize(name))));

    public ValueTask DisposeAsync() => Provider.DisposeAsync();
}
