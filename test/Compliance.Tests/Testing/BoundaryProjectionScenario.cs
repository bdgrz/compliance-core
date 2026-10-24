using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Publishes actual native source facts, then projects compact identities and reads exact source bodies.</summary>
sealed class BoundaryProjectionScenario : IAsyncDisposable
{
    readonly ServiceProvider _provider;
    readonly AsyncServiceScope _scope;
    public InMemoryKvClient Kv { get; } = new();
    public IImmutableContentStore Content { get; }
    public IAggregateReader Reader { get; }
    public IAggregateWriter Writer { get; }
    public InMemoryEventStore Events { get; } = new();
    public BoundaryReader Boundaries { get; }

    public BoundaryProjectionScenario()
    {
        var services = new ServiceCollection();
        Content = new FitzKvImmutableContentStore(Kv);
        services.AddSingleton<IEventStore>(Events);
        services.AddSingleton<IDomainEventReader>(Events);
        services.AddPortia();
        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateAsyncScope();
        Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        Boundaries = new(Reader, Events, Content);
    }

    readonly Dictionary<Uuid, Uuid> _successorCommands = [];
    public Uuid NewSuccessorVersion()
    {
        var command = Uuid.CreateVersion4();
        var version = Uuid.CreateVersion5(command, "draft");
        _successorCommands.Add(version, command);
        return version;
    }
    public Uuid SuccessorCommand(Uuid version) => _successorCommands[version];

    public async Task<IReadOnlyList<DomainEvent>> PublishAsync(BoundaryRules rules)
    {
        var before = (await ReadAsync(rules.TenantId, rules.Id)).Count;
        await BoundarySeeds.AppendAsync(Reader, Writer, Content, rules, CancellationToken.None);
        return (await ReadAsync(rules.TenantId, rules.Id)).Skip(before).ToArray();
    }

    public async Task<DomainEvent> PublishExplicitAsync(BoundaryRules after, IBoundaryMutationContent change,
        string kind, Uuid command, Uuid member, string display, DateTimeOffset at)
    {
        var owner = await Reader.HydrateAsync(new SystemBoundary(after.TenantId, after.Id));
        var before = owner.Header is { } prior ? (await Boundaries.RulesAsync(after.TenantId, after.Id)).Authority(prior) : null;
        ulong? gate = change switch
        {
            BoundaryDraftRevisedContent revised => unchecked((ulong)(revised.Revision - 1)),
            BoundaryReviewedContent reviewed => unchecked((ulong)reviewed.Revision),
            BoundaryApprovedContent approved => unchecked((ulong)approved.Revision),
            BoundaryDraftDiscardedContent discarded => unchecked((ulong)discarded.Revision),
            BoundarySuccessorProposedContent => unchecked((ulong)owner.Revision),
            _ => null,
        };
        var operation = OwnerProvenance.For(owner, command, kind, ActorReference.ForMember(member, display), owner.Id.ToString(),
            gate, AuthoredIntentDigest.Compute(change), at);
        var fact = await BoundarySeeds.FreezeAsync(Content, after, [change], operation, owner.NativeSequence + 1, CancellationToken.None);
        Assert.True(owner.Append(fact, owner.NativeSequence, owner.CommittedStreamPosition,
            new BoundaryMutationContent([change]), before, after.Authority(((IBoundaryFact)fact).Header)).IsSuccess);
        await Writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System));
        after.SourcePhysicalPosition = owner.CommittedStreamPosition;
        after.ClearPendingChanges();
        return fact;
    }

    public async Task<IReadOnlyList<DomainEvent>> ReadAsync(Uuid tenant, Uuid? draft = null)
    {
        var facts = new List<DomainEvent>();
        await foreach (var record in Events.ReadAsync(EventStreamPattern.ForPattern(tenant.ToString(), SystemBoundary.Area,
            draft?.ToString()), EventCursor.Start, CancellationToken.None))
            if (!record.Event.Metadata.IsAudit)
                facts.Add(record.Event);
        return facts;
    }

    public async Task ProjectAsync(IProjectionStore store, Func<DomainEvent, CancellationToken, ValueTask> apply,
        string name, Uuid tenant, IEnumerable<DomainEvent>? facts = null, ProjectionCheckpoint? checkpoint = null)
    {
        await using var batch = await store.BeginAsync(new ProjectionBatchContext(new CheckpointIdentity(name,
            EventStreamPattern.ForPattern(tenant.ToString(), SystemBoundary.Area)), ProjectionCheckpoint.Start));
        foreach (var fact in facts ?? await ReadAsync(tenant))
            await apply(fact, CancellationToken.None);
        await batch.CommitAsync(checkpoint ?? ProjectionCheckpoint.Start);
    }

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
    }
}
