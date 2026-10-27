using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.History;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
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
using Bdgrz.Compliance.Features.Evaluations.Aggregates;
using Bdgrz.Compliance.Features.Evaluations.Commands;
using Bdgrz.Compliance.Features.Evaluations.Events;
using Bdgrz.Compliance.Features.Evaluations.Handlers;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Evaluations.Readers;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Evaluations.Tests;

public sealed class ControlEvaluationReplayCutTests
{
    [Theory]
    [InlineData("payload")]
    [InlineData("event_id")]
    [InlineData("truncate")]
    public async Task ShouldRejectDifferentRawPrefixGivenActualHydratedEvaluationCut(string fault)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var state = await SeedAsync(fixture);
        var facts = Alternate(state.Facts, fault);

        // Act
        var error = await Record.ExceptionAsync(async () => _ = await ControlEvaluationHistory.ReadAsync(
            new Records(state.Owner.Stream, facts), state.Owner));

        // Assert
        if (fault == "truncate")
            Assert.IsType<ImmutableContentUnavailableException>(error);
        else
            Assert.IsType<ImmutableContentIntegrityException>(error);
        Assert.Equal(3, state.Owner.Revision);
    }

    [Fact]
    public async Task ShouldRejectDifferentPreparedPrefixGivenSameVersionTailAndLatestFact()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var state = await SeedAsync(fixture);
        var store = new InMemoryEventStore();
        await store.AppendAsync(state.Owner.Stream, 0, Alternate(state.Facts, "event_id"));
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(store);
        services.AddSingleton<IDomainEventReader>(store);
        services.AddPortia();
        services.AddVerifiedEvaluationReader();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var alternateReader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var alternate = await alternateReader.HydrateAsync(new ControlEvaluation(fixture.TenantId, state.Owner.Id));
        await using var originalScope = fixture.Provider.CreateAsyncScope();
        var original = originalScope.ServiceProvider;
        var reader = original.GetRequiredService<IAggregateReader>();
        var native = new NativeOperationExecutor(reader, scope.ServiceProvider.GetRequiredService<IAggregateExecutor>(),
            fixture.Content);
        var handler = new RecordControlEvaluationStepHandler(native, original.GetRequiredService<ControlEvaluationReader>(),
            fixture.Content, TimeProvider.System);
        var command = ControlEvaluationTests.Record(fixture, state.View, 0);
        var context = new InventoryRequestContext<RecordControlEvaluationStep>(command,
            ProgramManagementServices.Actor(fixture.OwnerUserId));

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);
        var after = await alternateReader.HydrateAsync(new ControlEvaluation(fixture.TenantId, state.Owner.Id));

        // Assert
        Assert.Equal(state.Owner.Version, alternate.Version);
        Assert.Equal(state.Owner.CommittedStreamPosition, alternate.CommittedStreamPosition);
        Assert.Equal(state.Owner.Latest, alternate.Latest);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(result.Error!.IsTransient);
        Assert.Equal(3, after.Revision);
    }

    [Fact]
    public async Task ShouldPreserveCapturedEarlierCutGivenLegitimateLaterStepCorrection()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var state = await SeedAsync(fixture);
        await fixture.AsAsync(fixture.OwnerUserId, ControlEvaluationTests.Record(fixture, state.View, 0));

        // Act
        var history = await ControlEvaluationHistory.ReadAsync(fixture.Provider.GetRequiredService<IDomainEventReader>(),
            state.Owner);

        // Assert
        Assert.Equal(3, history.Count);
        Assert.Equal(3, state.Owner.Revision);
    }

    static async Task<Seed> SeedAsync(OperationsFixture fixture)
    {
        var current = await ControlEvaluationTests.StartAsync(fixture);
        current = await fixture.AsAsync(fixture.OwnerUserId, ControlEvaluationTests.Record(fixture, current, 0));
        current = await fixture.AsAsync(fixture.OwnerUserId, ControlEvaluationTests.Record(fixture, current, 1));
        var owner = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new ControlEvaluation(fixture.TenantId, current.EvaluationId));
        var facts = new List<DomainEvent>();
        await foreach (var record in fixture.Provider.GetRequiredService<IDomainEventReader>().ReadAsync(owner.Stream, 0,
                           CancellationToken.None))
            facts.Add(record.Event);
        return new Seed(current, owner, facts.ToArray());
    }

    static DomainEvent[] Alternate(DomainEvent[] originalFacts, string fault)
    {
        if (fault == "truncate")
            return originalFacts[..^1];
        var facts = originalFacts.ToArray();
        var original = Assert.IsType<ControlEvaluationStepRecorded>(facts[1]);
        var changed = fault == "payload" ? original with { StepId = Uuid.CreateVersion4() } : original with { };
        changed.AttachMetadata(fault == "event_id"
            ? original.Metadata with { EventId = Uuid.CreateVersion4() } : original.Metadata);
        facts[1] = changed;
        return facts;
    }

    sealed record Seed(ControlEvaluationView View, ControlEvaluation Owner, DomainEvent[] Facts);

    sealed class Records(EventStreamAddress stream, DomainEvent[] facts) : IDomainEventReader
    {
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress address, ulong fromOffset,
            CancellationToken ct = default) => Enumerate(address, fromOffset, ct);
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            CancellationToken ct = default) => Enumerate(stream, 0, ct);
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
