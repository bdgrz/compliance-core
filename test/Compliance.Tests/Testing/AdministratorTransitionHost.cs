using System.Collections.Concurrent;
using System.Security.Claims;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Composes the guard-first administrator transition end to end over in-memory Fitz and an in-memory event
///     store: the five public routes, the eight receivers on the real request bus, the process kernel and the
///     test source-fact resolver. A test admits a transition, then drives the process when it wants the source
///     to move, which is how a paused receiver is exercised.
/// </summary>
sealed class AdministratorTransitionHost : IAsyncDisposable
{
    public static readonly DateTimeOffset At = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    readonly GatedReader _gate;
    readonly AdministratorTransitionAdmission _admissions;
    readonly TenantManagerGuardLoader _guards;
    readonly TimeProvider _clock = TimeProvider.System;

    public AdministratorTransitionHost()
    {
        var services = new ServiceCollection();
        var events = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddSingleton<IProjectionCheckpointStore, InMemoryProjectionCheckpointStore>();
        services.AddSingleton(TimeProvider.System);
        var kv = new InMemoryKvClient();
        services.AddSingleton<IImmutableContentStore>(new FitzKvImmutableContentStore(new PersistenceKvClient(kv)));
        services.AddSingleton<IProcessRecordStore>(new FitzKvProcessRecordStore(new PersistenceKvClient(kv)));
        services.AddSingleton(AdministratorTransitionProcess.Definition);
        services.AddSingleton<ProcessCatalog>(provider => new ProcessCatalog(provider.GetServices<ProcessDefinition>()));
        services.AddSingleton<ISourceFactResolver>(provider =>
            new EventStoreSourceFactResolver(provider.GetRequiredService<IDomainEventReader>()));
        services.AddScoped<NativeOperationExecutor>();
        services.AddScoped<ProcessDriver>();
        services.AddScoped<ProcessRootReaction>();
        services.AddScoped<AdministratorReceiverSupport>();
        services.AddScoped<TenantManagerGuardBootstrap>();
        services.AddScoped<TenantManagerGuardLoader>();
        services.AddPortia()
            .AddRequestHandler<ApplyMemberWithdrawalHandler>()
            .AddRequestHandler<ApplyAdministratorAssignmentWithdrawalHandler>()
            .AddRequestHandler<ApplyMemberRestorationHandler>()
            .AddRequestHandler<ApplyAdministratorAssignmentRestorationHandler>()
            .AddRequestHandler<ApplyNeutralMemberSourceMutationHandler>()
            .AddRequestHandler<ApplyNeutralAdministratorAssignmentMutationHandler>()
            .AddRequestHandler<CompleteAdministratorWithdrawalHandler>()
            .AddRequestHandler<CompleteAdministratorRestorationHandler>()
            .AddRequestAuthorizer<AdministratorTransitionReceiverAuthorizer>();
        Provider = services.BuildServiceProvider();
        Events = events;
        Content = Provider.GetRequiredService<IImmutableContentStore>();
        Store = Provider.GetRequiredService<IProcessRecordStore>();
        _gate = new GatedReader(Provider.GetRequiredService<IAggregateReader>());
        var executor = Provider.GetRequiredService<IAggregateExecutor>();
        _guards = new TenantManagerGuardLoader(_gate, Content, new TenantManagerGuardBootstrap(events, _gate, _clock));
        _admissions = new AdministratorTransitionAdmission(_guards, new NativeOperationExecutor(_gate, executor, Content),
            Content);
    }

    public ServiceProvider Provider { get; }

    public InMemoryEventStore Events { get; }

    public IImmutableContentStore Content { get; }

    public IProcessRecordStore Store { get; }

    public Uuid TenantId { get; } = Uuid.CreateVersion4();

    public Uuid AdministratorsTeamId => BuiltInRbac.AdministratorsTeamId(TenantId);

    /// <summary>Holds the second guard hydration of the next <paramref name="count" /> admissions until all have read it.</summary>
    public void HoldGuardReads(int count) => _gate.Hold(count);

    /// <summary>How many times any public route hydrated the tenant manager guard.</summary>
    public int GuardReads => _gate.GuardReads;

    /// <summary>Registers a member who is not an administrator.</summary>
    public async Task<Person> AddMemberAsync()
    {
        var user = Uuid.CreateVersion4();
        await SeedAsync(new Member(TenantId, user), member => member.Register());
        var registered = await Reader.HydrateAsync(new Member(TenantId, user));
        return new Person(user, registered.Id, registered.MembershipEpisodeId);
    }

    /// <summary>Registers a member assigned to the administrators team directly in the source, as a tenant that predates the guard.</summary>
    public async Task<Person> AddAdministratorAsync()
    {
        var person = await AddMemberAsync();
        await SeedAsync(new TeamMember(TenantId, AdministratorsTeamId, person.MemberId),
            assignment => assignment.Assign(person.EpisodeId));
        return person;
    }

    public IAggregateReader Reader => Provider.GetRequiredService<IAggregateReader>();

    public async Task<Member> MemberAsync(Person person) =>
        await Reader.HydrateAsync(new Member(TenantId, person.UserId));

    public async Task<TeamMember> AssignmentAsync(Person person, Uuid? teamId = null) =>
        await Reader.HydrateAsync(new TeamMember(TenantId, teamId ?? AdministratorsTeamId, person.MemberId));

    /// <summary>The guard as the next admission would see it: the pinned contribution set plus every fact since.</summary>
    public async Task<TenantManagerGuard> GuardAsync() =>
        (await new TenantManagerGuardLoader(Reader, Content, new TenantManagerGuardBootstrap(Events, Reader, _clock))
            .LoadAsync(TenantId, default, CancellationToken.None)).Guard;

    public async Task<bool> IsAvailableAsync(Person person) => (await GuardAsync()).IsAvailable(person.EpisodeId);

    public async Task<List<DomainEvent>> GuardFactsAsync() => await FactsAsync(new TenantManagerGuard(TenantId).Stream);

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

    // --- public routes

    /// <summary>The authored request a client holding the current revisions would send.</summary>
    public async Task<SuspendMember> SuspendRequestAsync(Person target, Uuid? commandId = null,
        string reason = "Employment ended.") => new(commandId ?? Uuid.CreateVersion4(), TenantId, target.UserId, reason,
        target.EpisodeId, (long)(await MemberAsync(target)).MemberRevision);

    public async Task<DeprovisionMember> DeprovisionRequestAsync(Person target, Uuid? commandId = null,
        string reason = "Access is no longer required.") => new(commandId ?? Uuid.CreateVersion4(), TenantId,
        target.UserId, reason, target.EpisodeId, (long)(await MemberAsync(target)).MemberRevision);

    public async Task<ReinstateMember> ReinstateRequestAsync(Person target, Uuid? commandId = null) =>
        new(commandId ?? Uuid.CreateVersion4(), TenantId, target.UserId, target.EpisodeId,
            (long)(await MemberAsync(target)).MemberRevision);

    public async Task<AssignTeamMember> AssignRequestAsync(Person target, Uuid? teamId = null, Uuid? commandId = null)
    {
        var assignment = await AssignmentAsync(target, teamId);
        return new AssignTeamMember(commandId ?? Uuid.CreateVersion4(), TenantId, teamId ?? AdministratorsTeamId,
            target.MemberId, target.EpisodeId, (long)assignment.AssignmentRevision, (long)assignment.AssignmentGeneration);
    }

    public async Task<RemoveTeamMember> RemoveRequestAsync(Person target, Uuid? teamId = null, Uuid? commandId = null)
    {
        var assignment = await AssignmentAsync(target, teamId);
        return new RemoveTeamMember(commandId ?? Uuid.CreateVersion4(), TenantId, teamId ?? AdministratorsTeamId,
            target.MemberId, target.EpisodeId, (long)assignment.AssignmentRevision, (long)assignment.AssignmentGeneration);
    }

    /// <summary>Sends any of the five public requests through its handler as <paramref name="actor" />.</summary>
    public Task<Result<AdministratorTransitionResponse>> SubmitAsync(Person actor, IRequestBase request) => request switch
    {
        SuspendMember suspend => new SuspendMemberHandler(_gate, _admissions, _clock).HandleAsync(
            new RequestContext<SuspendMember>(suspend, Actor(actor)), CancellationToken.None).AsTask(),
        DeprovisionMember deprovision => new DeprovisionMemberHandler(_gate, _admissions, _clock).HandleAsync(
            new RequestContext<DeprovisionMember>(deprovision, Actor(actor)), CancellationToken.None).AsTask(),
        ReinstateMember reinstate => new ReinstateMemberHandler(_gate, _admissions, _clock).HandleAsync(
            new RequestContext<ReinstateMember>(reinstate, Actor(actor)), CancellationToken.None).AsTask(),
        AssignTeamMember assign => AssignHandler().HandleAsync(new RequestContext<AssignTeamMember>(assign, Actor(actor)),
            CancellationToken.None).AsTask(),
        RemoveTeamMember remove => RemoveHandler().HandleAsync(new RequestContext<RemoveTeamMember>(remove, Actor(actor)),
            CancellationToken.None).AsTask(),
        _ => throw new ArgumentException("Unsupported request.", nameof(request)),
    };

    public async Task<Result<AdministratorTransitionResponse>> SuspendAsync(Person actor, Person target,
        Uuid? commandId = null, string reason = "Employment ended.", long? revision = null, Uuid? episode = null)
    {
        var member = await MemberAsync(target);
        return await new SuspendMemberHandler(_gate, _admissions, _clock).HandleAsync(
            new RequestContext<SuspendMember>(new SuspendMember(commandId ?? Uuid.CreateVersion4(), TenantId,
                target.UserId, reason, episode ?? target.EpisodeId, revision ?? (long)member.MemberRevision),
                Actor(actor)), CancellationToken.None);
    }

    public async Task<Result<AdministratorTransitionResponse>> DeprovisionAsync(Person actor, Person target,
        Uuid? commandId = null, string reason = "Access is no longer required.", long? revision = null)
    {
        var member = await MemberAsync(target);
        return await new DeprovisionMemberHandler(_gate, _admissions, _clock).HandleAsync(
            new RequestContext<DeprovisionMember>(new DeprovisionMember(commandId ?? Uuid.CreateVersion4(), TenantId,
                target.UserId, reason, target.EpisodeId, revision ?? (long)member.MemberRevision), Actor(actor)),
            CancellationToken.None);
    }

    public async Task<Result<AdministratorTransitionResponse>> ReinstateAsync(Person actor, Person target,
        Uuid? commandId = null, long? revision = null)
    {
        var member = await MemberAsync(target);
        return await new ReinstateMemberHandler(_gate, _admissions, _clock).HandleAsync(
            new RequestContext<ReinstateMember>(new ReinstateMember(commandId ?? Uuid.CreateVersion4(), TenantId,
                target.UserId, target.EpisodeId, revision ?? (long)member.MemberRevision), Actor(actor)),
            CancellationToken.None);
    }

    public async Task<Result<AdministratorTransitionResponse>> AssignAsync(Person actor, Person target,
        Uuid? teamId = null, Uuid? commandId = null)
    {
        var assignment = await AssignmentAsync(target, teamId);
        return await AssignHandler().HandleAsync(new RequestContext<AssignTeamMember>(new AssignTeamMember(
            commandId ?? Uuid.CreateVersion4(), TenantId, teamId ?? AdministratorsTeamId, target.MemberId,
            target.EpisodeId, (long)assignment.AssignmentRevision, (long)assignment.AssignmentGeneration), Actor(actor)),
            CancellationToken.None);
    }

    public async Task<Result<AdministratorTransitionResponse>> RemoveAsync(Person actor, Person target,
        Uuid? teamId = null, Uuid? commandId = null)
    {
        var assignment = await AssignmentAsync(target, teamId);
        return await RemoveHandler().HandleAsync(new RequestContext<RemoveTeamMember>(new RemoveTeamMember(
            commandId ?? Uuid.CreateVersion4(), TenantId, teamId ?? AdministratorsTeamId, target.MemberId,
            target.EpisodeId, (long)assignment.AssignmentRevision, (long)assignment.AssignmentGeneration), Actor(actor)),
            CancellationToken.None);
    }

    AssignTeamMemberHandler AssignHandler()
    {
        var content = Content;
        var executor = Provider.GetRequiredService<IAggregateExecutor>();
        return new AssignTeamMemberHandler(new NativeOperationExecutor(_gate, executor, content), _admissions, _gate,
            new MemberAuthorityCleanup(_gate, executor, Events, new InMemoryProjectionCheckpointStore(), _clock),
            content, _clock);
    }

    RemoveTeamMemberHandler RemoveHandler()
    {
        var executor = Provider.GetRequiredService<IAggregateExecutor>();
        return new RemoveTeamMemberHandler(new NativeOperationExecutor(_gate, executor, Content), _admissions, _gate,
            Content, _clock);
    }

    // --- the process

    /// <summary>Starts and drives the process of one admitted operation, exactly as its root-fact reactor would.</summary>
    public async Task<ProcessRecord> DriveAsync(Uuid operationId)
    {
        var record = await GuardRecordAsync(operationId);
        await using var scope = Provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ProcessRootReaction>().StartAsync(
            AdministratorTransitionProcess.Name, new RecordReactorContext(record), ((ITenantManagerGuardFact)record.Event).Intent,
            [], "test");
    }

    /// <summary>Creates the process record of an operation without driving it, as the reactor does before its first dispatch.</summary>
    public async Task<ProcessRecord> PrepareProcessAsync(Uuid operationId)
    {
        var record = await GuardRecordAsync(operationId);
        var fact = (ITenantManagerGuardFact)record.Event;
        return await Store.CreateIfAbsentAsync(ProcessRecord.Start(ProcessKeyOf(operationId),
            NativeFactReference.Of(record.Stream, record.Event), fact.Operation.Episode, fact.Intent, [],
            DateTimeOffset.UtcNow));
    }

    /// <summary>Builds the exact receiver command the driver would send for a step of the record.</summary>
    public async Task<IRequest<ReceiverOutcome>> CommandAsync(ProcessRecord record, string receiver)
    {
        var stage = receiver.StartsWith("Complete", StringComparison.Ordinal) ? "complete" : "apply";
        var step = new ProcessStep(record, stage, receiver, null, EffectIdentity.CommandId(record.Source.EventId,
            $"{record.Key.ProcessKind}:{receiver}", "-", record.Episode));
        return await AdministratorTransitionProcess.Definition.CreateReceiver(step, Provider, CancellationToken.None)
            ?? throw new InvalidOperationException($"{receiver} is not selected for this operation.");
    }

    /// <summary>Resumes an existing process record, as the sweep would.</summary>
    public async Task<ProcessRecord> ResumeAsync(Uuid operationId, string worker = "sweep")
    {
        await using var scope = Provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ProcessDriver>().RunAsync(ProcessKeyOf(operationId), worker,
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
    }

    public ProcessKey ProcessKeyOf(Uuid operationId) =>
        new(AdministratorTransitionProcess.Name, TenantId.ToString(), operationId);

    public Task<ProcessRecord?> ProcessAsync(Uuid operationId) => Store.ReadAsync(ProcessKeyOf(operationId));

    /// <summary>Sends one receiver command through the real bus as the system principal.</summary>
    public async Task<Result<ReceiverOutcome>> SendAsync(IRequest<ReceiverOutcome> command, ClaimsPrincipal? actor = null)
    {
        await using var scope = Provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        return await bus.SendAsync(command, new RequestDispatchContext(actor ?? RequestActor.System),
            CancellationToken.None);
    }

    /// <summary>The guard admission fact of an operation, as the reactor would see it.</summary>
    public async Task<DomainEventRecord> GuardRecordAsync(Uuid operationId)
    {
        await foreach (var record in Events.ReadAsync(new TenantManagerGuard(TenantId).Stream, 0))
        {
            if (record.Event is ITenantManagerGuardFact fact && fact.Operation.LogicalId == operationId &&
                record.Event is TenantManagerWithdrawalAccepted or TenantManagerRestorationAccepted
                    or TenantManagerSourceMutationAdmitted)
                return record;
        }

        throw new InvalidOperationException($"No guard admission carries operation {operationId}.");
    }

    public async Task<AdministratorTransitionIntent> IntentAsync(Uuid operationId)
    {
        var fact = (ITenantManagerGuardFact)(await GuardRecordAsync(operationId)).Event;
        return await Content.ReadJsonAsync<AdministratorTransitionIntent>(TenantId.ToString(), fact.Intent);
    }

    public async Task SeedAsync<TAggregate>(TAggregate aggregate, Func<TAggregate, Result> operation)
        where TAggregate : Aggregate
    {
        var result = await Provider.GetRequiredService<IAggregateExecutor>().ExecuteAsync(aggregate,
            item => AggregateOutcome.Commit(operation(item)), new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    public static ClaimsPrincipal Actor(Person person) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", person.UserId.ToString())], "BdgrzSession"));

    public ValueTask DisposeAsync() => Provider.DisposeAsync();

    /// <summary>One registered member of the host's tenant and the episode they hold.</summary>
    public sealed record Person(Uuid UserId, Uuid MemberId, Uuid EpisodeId);

    /// <summary>Holds the second hydration of each admission's guard until the requested number have reached it.</summary>
    sealed class GatedReader(IAggregateReader inner) : IAggregateReader
    {
        readonly ConcurrentDictionary<Uuid, int> _reads = new();
        readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int _remaining;
        int _guardReads;

        public int GuardReads => Volatile.Read(ref _guardReads);

        public void Hold(int count) => Volatile.Write(ref _remaining, count);

        public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            var hydrated = await inner.HydrateAsync(aggregate, ct);
            if (aggregate is TenantManagerGuard)
                Interlocked.Increment(ref _guardReads);
            if (aggregate is TenantManagerGuard guard && guard.Acceptance.SelectedLogicalId != Uuid.Empty &&
                _reads.AddOrUpdate(guard.Acceptance.SelectedLogicalId, 1, static (_, count) => count + 1) == 2 &&
                Volatile.Read(ref _remaining) > 0)
            {
                if (Interlocked.Decrement(ref _remaining) == 0)
                    _released.TrySetResult();
                await _released.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            }

            return hydrated;
        }
    }

    /// <summary>The typed reaction context of a guard fact read back from the store.</summary>
    public static IReactorContext<TEvent> ReactionOf<TEvent>(DomainEventRecord source) where TEvent : DomainEvent =>
        new TypedRecordReactorContext<TEvent>(source);

    sealed class TypedRecordReactorContext<TEvent>(DomainEventRecord record) : RecordReactorContext(record),
        IReactorContext<TEvent> where TEvent : DomainEvent
    {
        public TEvent Trigger => (TEvent)Source.Event;
    }
}
