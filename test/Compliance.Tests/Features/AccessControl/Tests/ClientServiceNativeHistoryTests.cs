using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.History;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
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
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
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
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class ClientServiceNativeHistoryTests
{
    [Theory]
    [InlineData("payload")]
    [InlineData("event_id")]
    [InlineData("truncate")]
    public async Task ShouldRejectAlternateMiddleServicePrefixGivenCapturedClientAuthority(string fault)
    {
        // Arrange
        await using var host = new IndependenceHost();
        for (var index = 0; index < 3; index++)
            Assert.True((await host.SendAsync(new RecordNonattestService(host.Tenant, Uuid.CreateVersion4(), index,
                IndependenceHost.Service()))).IsSuccess);
        var owner = await host.HydrateAsync(new ClientServiceIndependence(host.Tenant));
        var facts = await FactsAsync(host, owner.Stream);
        if (fault == "truncate")
            facts = facts[..^1];
        else
        {
            var original = (NonattestServiceRecorded)facts[1];
            var changed = fault == "payload" ? original with { ServiceContent = original.ServiceContent with { ContentId = Uuid.CreateVersion4() } } : original with { };
            changed.AttachMetadata(fault == "event_id" ? original.Metadata with { EventId = Uuid.CreateVersion4() } : original.Metadata);
            facts[1] = changed;
        }

        // Act
        var failure = await Record.ExceptionAsync(async () => _ = await ClientServiceNativeHistory.ReadAsync(new Records(owner.Stream, facts), owner));

        // Assert
        if (fault == "truncate")
            Assert.IsType<ImmutableContentUnavailableException>(failure);
        else
            Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.Equal(3UL, owner.Epoch);
    }

    [Theory]
    [InlineData("scope")]
    [InlineData("physical")]
    [InlineData("event_id")]
    [InlineData("metadata")]
    [InlineData("payload")]
    [InlineData("missing")]
    public async Task ShouldRejectGlobalWitnessGivenDifferentTailTuplePayloadOrMetadata(string fault)
    {
        // Arrange
        await using var host = new IndependenceHost();
        Assert.True((await host.SendAsync(new RecordNonattestService(host.Tenant, Uuid.CreateVersion4(), 0,
            IndependenceHost.Service()))).IsSuccess);
        var owner = await host.HydrateAsync(new ClientServiceIndependence(host.Tenant));
        var events = host.Scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
        var resolver = new DifferentWitness(new EventStoreSourceFactResolver(events), fault);

        // Act
        var failure = await Record.ExceptionAsync(() => IndependenceSourceCut.CaptureAsync(events, resolver, owner,
            TimeProvider.System, CancellationToken.None));

        // Assert
        Assert.Equal(1, resolver.Calls);
        if (fault == "missing")
            Assert.IsType<ImmutableContentUnavailableException>(failure);
        else
            Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.Equal(1UL, owner.Epoch);
    }

    [Fact]
    public async Task ShouldSealEmptyGlobalCutWithoutResolverCallGivenEmptyCapturedOwner()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var owner = new ClientServiceIndependence(host.Tenant);
        var events = host.Scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
        var resolver = new DifferentWitness(new EventStoreSourceFactResolver(events), "missing");

        // Act
        var cut = await IndependenceSourceCut.CaptureAsync(events, resolver, owner, TimeProvider.System, CancellationToken.None);

        // Assert
        Assert.Equal(0UL, cut.GlobalWatermarkExclusive);
        Assert.Equal($"{owner.Stream.Realm}/{owner.Stream.Area}/{owner.Stream.Resource}", cut.AnchorSelector);
        Assert.Equal(0, resolver.Calls);
    }

    [Fact]
    public async Task ShouldFailClosedWithoutResolverCallGivenCapturedTailIsAuditRecord()
    {
        // Arrange
        await using var host = new IndependenceHost();
        Assert.True((await host.SendAsync(new RecordNonattestService(host.Tenant, Uuid.CreateVersion4(), 0,
            IndependenceHost.Service()))).IsSuccess);
        var captured = await host.HydrateAsync(new ClientServiceIndependence(host.Tenant));
        var original = captured.LatestService!;
        var audit = original with { };
        audit.AttachMetadata(original.Metadata with { EventId = Uuid.CreateVersion4(), IsAudit = true });
        var events = host.Scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
        var stream = new ClientServiceIndependence(host.Tenant).Stream;
        await host.Scope.ServiceProvider.GetRequiredService<IEventStore>().AppendAsync(stream, 1, [audit], CancellationToken.None);
        // Exercise a retained legacy physical cut directly: SDK 0.9.1 keeps current audit sessions
        // on separate streams and correctly refuses to hydrate an audit as a state change.
        typeof(Aggregate).GetProperty(nameof(Aggregate.CommittedStreamPosition))!.SetValue(captured, 2UL);
        var resolver = new DifferentWitness(new EventStoreSourceFactResolver(events), "missing");

        // Act
        var failure = await Record.ExceptionAsync(() => IndependenceSourceCut.CaptureAsync(events, resolver, captured,
            TimeProvider.System, CancellationToken.None));

        // Assert
        Assert.Equal(2UL, captured.CommittedStreamPosition);
        Assert.IsType<ImmutableContentUnavailableException>(failure);
        Assert.Equal(0, resolver.Calls);
    }

    static async Task<DomainEvent[]> FactsAsync(IndependenceHost host, EventStreamAddress stream)
    {
        var found = new List<DomainEvent>();
        await foreach (var record in host.Scope.ServiceProvider.GetRequiredService<IDomainEventReader>().ReadAsync(stream, 0))
            found.Add(record.Event);
        return found.ToArray();
    }

    sealed class DifferentWitness(ISourceFactResolver inner, string fault) : ISourceFactResolver
    {
        public int Calls { get; private set; }
        public async Task<ResolvedSourceFact> ResolveAsync(EventStreamAddress stream, ulong position, CancellationToken ct = default)
        {
            Calls++;
            if (fault == "missing")
                throw new SourceFactUnavailableException("The exact broker witness is unavailable.");
            var resolved = await inner.ResolveAsync(stream, position, ct);
            var locator = resolved.Locator;
            var body = resolved.Event;
            if (fault == "scope")
                locator = locator with { Source = new ClientServiceIndependence(Uuid.CreateVersion4()).Stream };
            if (fault == "physical")
                locator = locator with { ResourcePosition = position + 1 };
            if (fault == "event_id")
                locator = locator with { EventId = Uuid.CreateVersion4() };
            if (fault is "metadata" or "payload")
            {
                var original = (NonattestServiceRecorded)body;
                var changed = fault == "payload" ? original with { ServiceContent = original.ServiceContent with { Sha256 = new string('f', 64) } } : original with { };
                changed.AttachMetadata(fault == "metadata" ? original.Metadata with { EventId = Uuid.CreateVersion4() } : original.Metadata);
                body = changed;
            }
            return new ResolvedSourceFact(body, locator);
        }
        public Task<VerifiedSourceProof<TEvent>> VerifyAsync<TEvent>(SourceFactProof<TEvent> proof,
            CancellationToken ct = default) where TEvent : DomainEvent => throw new NotSupportedException();
    }

    sealed class Records(EventStreamAddress stream, DomainEvent[] facts) : IDomainEventReader
    {
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress address, ulong fromOffset, CancellationToken ct = default) => Enumerate(address, fromOffset, ct);
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor, CancellationToken ct = default) => Enumerate(stream, 0, ct);
        async IAsyncEnumerable<DomainEventRecord> Enumerate(EventStreamAddress address, ulong fromOffset,
            [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            if (address != stream)
                yield break;
            for (var index = fromOffset; index < (ulong)facts.Length; index++)
            {
                ct.ThrowIfCancellationRequested();
                yield return new DomainEventRecord(stream, facts[index], index, EventCursor.Start);
            }
        }
    }
}
