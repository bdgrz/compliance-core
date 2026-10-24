using Bdgrz.Compliance.Features.Controls;
using System.Security.Claims;
using Bdgrz.Compliance.Features.AuditPopulations;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     The full composition over in-memory stores for the Type II range process: handlers are built from the same
///     scope as the production ones, root facts are turned into process records the way the reactor does, and the
///     release reactor is driven from committed period facts.
/// </summary>
sealed class TypeIIRangeHost : IAsyncDisposable
{
    public TypeIIRangeHost(TimeProvider? clock = null, Func<IProcessRecordStore, IProcessRecordStore>? store = null)
    {
        var services = new ServiceCollection();
        var values = new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance",
        };
        services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
            developerAuthentication: true);
        var events = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddSingleton<IKvClient>(new InMemoryKvClient());
        services.AddSingleton<IPlatformUserDirectoryReader>(new ExistingUsers());
        if (clock is not null)
            services.AddSingleton(clock);
        if (store is not null)
        {
            services.RemoveAll<IProcessRecordStore>();
            services.AddSingleton<IProcessRecordStore>(provider =>
                store(new FitzKvProcessRecordStore(provider.GetRequiredService<IKvClient>())));
        }

        _owned = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        Provider = _owned;
        Scope = Provider.CreateAsyncScope();
    }

    /// <summary>Wraps a running application's services; the caller keeps ownership of them.</summary>
    public TypeIIRangeHost(IServiceProvider services)
    {
        Provider = services;
        Scope = Provider.CreateAsyncScope();
    }

    readonly ServiceProvider? _owned;

    public IServiceProvider Provider { get; }

    public AsyncServiceScope Scope { get; }

    public IServiceProvider Services => Scope.ServiceProvider;

    public IAggregateReader Reader => Services.GetRequiredService<IAggregateReader>();

    public IAggregateWriter Writer => Services.GetRequiredService<IAggregateWriter>();

    public IAggregateExecutor Executor => Services.GetRequiredService<IAggregateExecutor>();

    public IDomainEventReader Events => Services.GetRequiredService<IDomainEventReader>();

    public IImmutableContentStore Content => Services.GetRequiredService<IImmutableContentStore>();

    public TypeIIPeriodReader Periods => new(Reader, Events, Content);
    public TypeIIPeriodOperations PeriodOperations(TimeProvider clock) => new(
        Services.GetRequiredService<NativeOperationExecutor>(), Periods, Content, clock);

    public IRequestBus Bus => Services.GetRequiredService<IRequestBus>();

    public IProcessRecordStore Records => Services.GetRequiredService<IProcessRecordStore>();

    public ProcessDefinition Definition => Provider.GetRequiredService<ProcessCatalog>()
        .Get(TypeIIRangeEffects.ProcessName);

    public StartTypeIIObservationPeriodHandler StartHandler(TimeProvider clock) => new(
        Services.GetRequiredService<NativeOperationExecutor>(), Reader, new ControlReader(Reader, Events, Content),
        Services.GetRequiredService<FindingReader>(), Services.GetRequiredService<ControlOperationsReader>(), Content,
        clock);

    public AmendTypeIIObservationPeriodHandler AmendHandler(TimeProvider clock) => new(
        Services.GetRequiredService<NativeOperationExecutor>(), Reader, Periods, Content, clock);

    public CancelTypeIIPeriodAmendmentHandler CancelHandler(TimeProvider clock) => new(
        Services.GetRequiredService<NativeOperationExecutor>(), Content, clock);

    public GenerateDueControlOccurrencesHandler GenerateHandler(TimeProvider clock) => new(
        PeriodOperations(clock), Services.GetRequiredService<ControlOperationsReader>(), Content);

    public Task<ProcessKey> FanOutKeyOf(Uuid tenantId, Uuid operationId) =>
        ProcessKeys.FindAsync(Provider, ControlOccurrenceFanOut.ProcessName, tenantId.ToString(), operationId);

    /// <summary>Runs the fan-out root reactor on the committed selections of a period, as the worker would.</summary>
    public async Task ReactFanOutAsync(EventStreamAddress stream)
    {
        await using var scope = Provider.CreateAsyncScope();
        var reactor = scope.ServiceProvider.GetRequiredService<ControlOccurrenceFanOutReactor>();
        await foreach (var record in scope.ServiceProvider.GetRequiredService<IDomainEventReader>().ReadAsync(stream, 0))
            if (record.Event is DueControlOccurrencesGenerated)
                await reactor.HandleAsync(AdministratorTransitionHost.ReactionOf<DueControlOccurrencesGenerated>(record),
                    CancellationToken.None);
    }

    public ProcessDefinition FanOutDefinition => Provider.GetRequiredService<ProcessCatalog>()
        .Get(ControlOccurrenceFanOut.ProcessName);

    /// <summary>Creates (if absent) the fan-out record of a committed selection, as the root reactor does.</summary>
    public async Task<ProcessRecord> StartFanOutAsync(Uuid tenantId, Uuid programId, Uuid periodId,
        DueControlOccurrencesGenerated selected)
    {
        var period = await HydrateAsync(new TypeIIPeriod(tenantId, programId, periodId));
        var manifest = await Content.ReadJsonAsync<ControlOccurrenceSelectionManifest>(tenantId.ToString(),
            selected.CreationIntentManifest);
        return await Records.CreateIfAbsentAsync(ProcessRecord.Start(ProcessKey.ForRoot(ControlOccurrenceFanOut.ProcessName, selected.Operation.RootOperationId,
            NativeFactReference.Of(period.Stream, selected)), NativeFactReference.Of(period.Stream, selected),
            selected.Operation.Episode, selected.CreationIntentManifest,
            manifest.Selections.Select(static selection => selection.OccurrenceId.ToString()),
            DateTimeOffset.UtcNow));
    }

    /// <summary>Starts (if absent) and drives the fan-out of a committed selection to its end.</summary>
    public async Task<ProcessRecord> RunFanOutAsync(Uuid tenantId, Uuid programId, Uuid periodId,
        DueControlOccurrencesGenerated selected)
    {
        var started = await StartFanOutAsync(tenantId, programId, periodId, selected);
        await using var scope = Provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ProcessDriver>().RunAsync(started.Key, "test-worker",
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
    }

    /// <summary>The receiver command the fan-out builds for one target, with the driver's deterministic command id.</summary>
    public async Task<MaterializeControlOccurrence> FanOutStepAsync(ProcessRecord record, string target) =>
        (MaterializeControlOccurrence)(await FanOutDefinition.CreateReceiver(new ProcessStep(record,
            ControlOccurrenceFanOut.Materialize, nameof(MaterializeControlOccurrence), target,
            ControlOccurrenceFanOut.ReceiverCommandId(record.Source.EventId, target, record.Episode)), Services,
            CancellationToken.None))!;

    public ValueTask<Result<ReceiverOutcome>> SendAsync(IRequest<ReceiverOutcome> command) =>
        Bus.DispatchAsync(command, new RequestDispatchContext(RequestActor.System), CancellationToken.None);

    public async Task<T> HydrateAsync<T>(T aggregate) where T : Aggregate =>
        await Reader.HydrateAsync(aggregate);

    public Task<ProcessKey> KeyOf(Uuid tenantId, Uuid operationId) =>
        ProcessKeys.FindAsync(Provider, TypeIIRangeEffects.ProcessName, tenantId.ToString(), operationId);

    /// <summary>Creates (if absent) the process record of an accepted opening or amendment, as the root reactor does.</summary>
    public async Task<ProcessRecord> StartProcessAsync(Uuid tenantId, Uuid programId, Uuid periodId, Uuid operationId)
    {
        var period = await HydrateAsync(new TypeIIPeriod(tenantId, programId, periodId));
        var accepted = period.AcceptedFor(operationId) ?? throw new InvalidOperationException("Not accepted.");
        return await Records.CreateIfAbsentAsync(ProcessRecord.Start(ProcessKey.ForRoot(TypeIIRangeEffects.ProcessName, operationId,
                NativeFactReference.Of(period.Stream, accepted)),
            NativeFactReference.Of(period.Stream, accepted), ((INativeOperationFact)accepted).Operation.Episode,
            TypeIIPeriod.IntentOf(accepted)!, [], DateTimeOffset.UtcNow));
    }

    /// <summary>Starts (if absent) and drives the process of an accepted opening or amendment.</summary>
    public async Task<ProcessRecord> RunProcessAsync(Uuid tenantId, Uuid programId, Uuid periodId, Uuid operationId)
    {
        var started = await StartProcessAsync(tenantId, programId, periodId, operationId);
        await using var scope = Provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ProcessDriver>().RunAsync(started.Key, "test-worker",
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
    }

    /// <summary>The receiver command a step of the record builds, with the deterministic command id of the driver.</summary>
    public async Task<IRequest<ReceiverOutcome>> StepAsync(ProcessRecord record, string stage, string receiver) =>
        (await Definition.CreateReceiver(new ProcessStep(record, stage, receiver, null,
            TypeIIRangeEffects.ReceiverCommandId(record.Source.EventId, receiver, record.Episode)), Services,
            CancellationToken.None))!;

    /// <summary>Dispatches a receiver in its own scope, as a concurrent worker would.</summary>
    public async Task<Result<ReceiverOutcome>> SendInNewScopeAsync(IRequest<ReceiverOutcome> command)
    {
        await using var scope = Provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(command,
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
    }

    /// <summary>Runs the release reactor on the committed period facts of one kind, as the worker would.</summary>
    public async Task ReleaseAsync<TEvent>(EventStreamAddress stream, Func<TEvent, bool>? match = null)
        where TEvent : DomainEvent
    {
        await using var scope = Provider.CreateAsyncScope();
        var reactor = scope.ServiceProvider.GetRequiredService<TypeIIRangeReleaseReactor>();
        await foreach (var record in scope.ServiceProvider.GetRequiredService<IDomainEventReader>().ReadAsync(stream, 0))
        {
            if (record.Event is not TEvent typed || match is not null && !match(typed))
                continue;
            switch (record.Event)
            {
                case TypeIIPeriodAmendmentCanceled:
                    await reactor.HandleAsync(AdministratorTransitionHost.ReactionOf<TypeIIPeriodAmendmentCanceled>(record),
                        CancellationToken.None);
                    break;
                case TypeIIPeriodOpeningRejected:
                    await reactor.HandleAsync(AdministratorTransitionHost.ReactionOf<TypeIIPeriodOpeningRejected>(record),
                        CancellationToken.None);
                    break;
                case ObservationPeriodCloseFrozen:
                    await reactor.HandleAsync(AdministratorTransitionHost.ReactionOf<ObservationPeriodCloseFrozen>(record),
                        CancellationToken.None);
                    break;
                default:
                    throw new InvalidOperationException($"{record.Event.GetType().Name} is not released by the reactor.");
            }
        }
    }

    public async Task<List<TEvent>> EventsAsync<TEvent>(EventStreamAddress stream) where TEvent : DomainEvent
    {
        var found = new List<TEvent>();
        try
        {
            await foreach (var record in Events.ReadAsync(stream, 0))
                if (record.Event is TEvent typed)
                    found.Add(typed);
        }
        catch (InvalidOperationException)
        {
            // The in-memory store reports an unwritten stream this way.
        }

        return found;
    }

    public async ValueTask DisposeAsync()
    {
        await Scope.DisposeAsync();
        if (_owned is not null)
            await _owned.DisposeAsync();
    }

    /// <summary>A principal that passes the personal HTTP actor check of the Type II handlers.</summary>
    public static ClaimsPrincipal Personal(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()), new Claim("name", "Period manager")],
        "BdgrzSession"));

    sealed class ExistingUsers : IPlatformUserDirectoryReader
    {
        public ValueTask<bool> ExistsAsync(Uuid userId, CancellationToken ct = default) => ValueTask.FromResult(true);
    }
}
