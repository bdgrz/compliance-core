using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Contracts;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Boundaries.Aggregates;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Boundaries.Readers;
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
