using Bdgrz.Compliance.Features.Controls.Aggregates;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Projects real native source facts into compact directory identities, then reads verified source bodies.</summary>
sealed class ControlProjectionScenario : IAsyncDisposable
{
    readonly ServiceProvider _provider;
    readonly AsyncServiceScope _scope;
    public InMemoryKvClient Kv { get; } = new();
    public IImmutableContentStore Content { get; }
    public IAggregateReader Reader { get; }
    public IAggregateWriter Writer { get; }
    public IDomainEventReader Events { get; }
    public ControlReader Controls { get; }

    public ControlProjectionScenario()
    {
        var services = new ServiceCollection();
        var events = new InMemoryEventStore();
        Content = new FitzKvImmutableContentStore(Kv);
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddSingleton(Content);
        services.AddPortia();
        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateAsyncScope();
        Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        Events = events;
        Controls = new(Reader, events, Content);
    }

    public async Task<IReadOnlyList<DomainEvent>> PublishAsync(ControlRules rules)
    {
        var before = (await ReadAsync(rules.TenantId, rules.Id)).Count;
        await ControlSeeds.AppendAsync(Reader, Writer, Content, rules, CancellationToken.None);
        return (await ReadAsync(rules.TenantId, rules.Id)).Skip(before).ToArray();
    }

    /// <summary>Publishes unchanged legacy business data through its exact native envelope, retaining omitted actors.</summary>
    public async Task<DomainEvent> PublishExplicitAsync(ControlRules after, IControlMutationContent change,
        string commandKind, Uuid commandId, Uuid actorMemberId, DateTimeOffset at)
    {
        var owner = await Reader.HydrateAsync(new ControlDraft(after.TenantId, after.Id));
        var before = owner.IsCreated ? (await Controls.RulesAsync(after.TenantId, after.Id)).Authority(owner.Header!) : null;
        var operation = OwnerProvenance.For(owner, commandId, commandKind,
            ActorReference.ForMember(actorMemberId, actorMemberId.ToString()), owner.Id.ToString(),
            owner.IsCreated ? (ulong)owner.Revision : null, AuthoredIntentDigest.Compute(change), at);
        var fact = await ControlSeeds.FreezeAsync(Content, after, [change], operation, owner.NativeSequence + 1,
            CancellationToken.None);
        Assert.True(owner.Append(fact, owner.NativeSequence, owner.CommittedStreamPosition,
            new ControlMutationContent([change]), before, after.Authority(((IControlFact)fact).Header)).IsSuccess);
        await Writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System));
        after.SourcePhysicalPosition = owner.CommittedStreamPosition;
        after.ClearPendingChanges();
        return fact;
    }

    public async Task<IReadOnlyList<DomainEvent>> ReadAsync(Uuid tenantId, Uuid? controlId = null)
    {
        var values = new List<DomainEvent>();
        await foreach (var record in Events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(),
                           ControlDraft.Area, controlId?.ToString()), EventCursor.Start, CancellationToken.None))
            if (!record.Event.Metadata.IsAudit)
                values.Add(record.Event);
        return values;
    }

    public async Task ProjectAsync(IProjectionStore store, Func<DomainEvent, CancellationToken, ValueTask> apply,
        string projectorName, Uuid tenantId, IEnumerable<DomainEvent>? facts = null,
        ProjectionCheckpoint? checkpoint = null)
    {
        var selected = facts ?? await ReadAsync(tenantId);
        await using var batch = await store.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity(projectorName, EventStreamPattern.ForPattern(tenantId.ToString(), ControlDraft.Area)),
            ProjectionCheckpoint.Start));
        foreach (var fact in selected)
            await apply(fact, CancellationToken.None);
        await batch.CommitAsync(checkpoint ?? ProjectionCheckpoint.Start);
    }

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
    }
}
