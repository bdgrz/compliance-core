using System.Collections.Concurrent;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
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
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Operations.Events;
using Bdgrz.Compliance.Features.Operations.Queries;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Queries;
using Bdgrz.Compliance.Features.Work.Readers;
namespace Bdgrz.Compliance.Tests.Features.Work.Tests;

public sealed class ConcurrentOccurrenceSubmissionTests
{
    [Fact]
    public async Task ShouldRetainOneAttributionAndReviewItemGivenCompetingPersonalSubmissions()
    {
        // Arrange
        var source = await OperationsFixture.CreateAsync();
        await using var sourceProvider = source.Provider;
        await source.PlanAsync(backup: new OperatingHolder("member", source.BackupMemberId));
        var occurrence = (await source.OccurrencesAsync("missed"))[0];
        Assert.Equal(0, occurrence.Revision);
        var events = source.Provider.GetRequiredService<IEventStore>();
        var target = new ControlOccurrence(source.TenantId, source.ProgramId, source.ControlId,
            occurrence.OccurrenceId);
        var before = await ProgramManagementServices.HydrateAsync(source.Provider, target);
        var barrier = new CompetingAppendStore(events, target.Stream, before.CommittedStreamPosition);
        await using var provider = Compose(source, barrier);
        var owner = source.Attest(occurrence) with { Notes = "Owner submission" };
        var backup = source.Attest(occurrence) with { Notes = "Backup submission" };

        // Act
        var results = await Task.WhenAll(
            SubmitAsync(provider, source.OwnerUserId, owner),
            SubmitAsync(provider, source.BackupUserId, backup));
        var after = await ProgramManagementServices.HydrateAsync(provider,
            new ControlOccurrence(source.TenantId, source.ProgramId, source.ControlId, occurrence.OccurrenceId));
        var records = new List<DomainEventRecord>();
        await foreach (var record in events.ReadAsync(target.Stream, before.CommittedStreamPosition, CancellationToken.None))
            records.Add(record);
        await ProjectAsync(provider, source.TenantId);
        var queue = await ReviewWorkAsync(provider, source, source.ReviewerUserId);
        await using var replayProvider = Compose(source, events);
        await ProjectAsync(replayProvider, source.TenantId);
        await ProjectAsync(replayProvider, source.TenantId);
        var replayed = await ReviewWorkAsync(replayProvider, source, source.ReviewerUserId);
        var population = await PersonalOccurrenceProofTransportTests.HttpAsync(replayProvider, source.ReviewerUserId,
            new ListControlOccurrences(source.TenantId, source.ProgramId, source.ControlId));

        // Assert
        var winner = Assert.Single(results, result => result.Result is { IsSuccess: true }).Result!.Value;
        var loser = Assert.Single(results, result => result.Result is { IsSuccess: false }).Result!.Value;
        Assert.Equal(RequestErrorKind.Conflict, loser.Error!.Kind);
        Assert.Equal(2, barrier.Arrivals);
        Assert.All(barrier.ExpectedPositions, position => Assert.Equal(before.CommittedStreamPosition, position));
        Assert.All(barrier.BatchSizes, count => Assert.Equal(2, count));
        Assert.Equal(before.CommittedStreamPosition + 2, after.CommittedStreamPosition);
        Assert.Equal(2, records.Count);
        var opened = Assert.IsType<ControlOccurrenceOpened>(records[0].Event);
        var retained = Assert.IsType<ControlOccurrenceAttested>(records[1].Event);
        var attestation = Assert.Single(winner.Value.Attestations);
        Assert.Equal(retained.OccurrenceId, occurrence.OccurrenceId);
        var winningMember = results[0].Result is { IsSuccess: true } ? source.OwnerMemberId : source.BackupMemberId;
        Assert.Equal(winningMember.ToString(), opened.OpenedBy.Id);
        var winningUser = results[0].Result is { IsSuccess: true } ? source.OwnerUserId : source.BackupUserId;
        Assert.All(records, record => Assert.Equal(winningUser.ToString(), record.Event.Metadata.Actor?.Subject));
        Assert.Equal(winningMember, attestation.RecorderMemberId);
        Assert.Equal(new OperatingHolder("member", winningMember), attestation.PerformedBy);
        Assert.Equal(results[0].Result is { IsSuccess: true } ? owner.Notes : backup.Notes, attestation.Notes);
        Assert.Equal(occurrence.OccurrenceId, retained.OccurrenceId);
        Assert.Equal(1, attestation.Version);
        var current = Assert.Single(population.Value.Items, item => item.OccurrenceId == occurrence.OccurrenceId);
        Assert.Equal("submitted", current.State);
        Assert.Equal(WireJson.Of(attestation), WireJson.Of(Assert.Single(current.Attestations)));
        var item = Assert.Single(queue.Value.Items);
        Assert.Equal(occurrence.OccurrenceId, item.SourceId);
        Assert.Equal(1, queue.Value.Counts.Total);
        Assert.Equal(queue.Value.Counts, replayed.Value.Counts);
        Assert.Equal(item.WorkItemId, Assert.Single(replayed.Value.Items).WorkItemId);
    }

    static async Task<(Result<ControlOccurrenceView>? Result, EventStreamConcurrencyException? Conflict)> SubmitAsync(IServiceProvider provider, Uuid actor,
        AttestControlOccurrence request)
    {
        await using var scope = provider.CreateAsyncScope();
        try
        {
            return (await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
                PersonalReadinessClosureTransportTests.Context(actor, "http"), CancellationToken.None), null);
        }
        catch (EventStreamConcurrencyException exception)
        {
            // The bus propagates OCC; the generated HTTP binding owns its conflict response.
            return (null, exception);
        }
    }

    sealed class CompetingAppendStore(IEventStore inner, EventStreamAddress target, ulong expectedPosition) : IEventStore
    {
        readonly TaskCompletionSource _bothPrepared = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int _arrivals;
        public int Arrivals => _arrivals;
        public ConcurrentQueue<ulong> ExpectedPositions { get; } = new();
        public ConcurrentQueue<int> BatchSizes { get; } = new();

        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong fromOffset,
            CancellationToken ct) => inner.ReadAsync(stream, fromOffset, ct);

        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            CancellationToken ct) => inner.ReadAsync(pattern, cursor, ct);

        public async ValueTask AppendAsync(EventStreamAddress stream, ulong expectedStreamPosition,
            IReadOnlyList<DomainEvent> events, CancellationToken ct = default)
        {
            if (stream == target && events.Any(value => value is ControlOccurrenceAttested))
            {
                Assert.Equal(expectedPosition, expectedStreamPosition);
                ExpectedPositions.Enqueue(expectedStreamPosition);
                BatchSizes.Enqueue(events.Count);
                if (Interlocked.Increment(ref _arrivals) == 2)
                    _bothPrepared.SetResult();
                // Both production commands have prepared their batches before either reaches the real store.
                await _bothPrepared.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            }
            await inner.AppendAsync(stream, expectedStreamPosition, events, ct);
        }
    }

    static ServiceProvider Compose(OperationsFixture source, IEventStore events)
    {
        var services = new ServiceCollection();
        services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance"
        }).Build(), developerAuthentication: true);
        services.AddSingleton(events);
        services.AddSingleton<IDomainEventReader>((IDomainEventReader)events);
        services.AddSingleton<IKvClient>(new InMemoryKvClient());
        services.AddSingleton(source.Provider.GetRequiredService<IImmutableContentStore>());
        services.AddSingleton<IAccessGrantPermissionAuthorizer>(new PermissionBackedAccessGrantPermissionAuthorizer(source.Permissions));
        services.AddSingleton<IProgramResourceScopeResolver, TestProgramResourceScopeResolver>();
        services.AddSingleton<ITenantActivity, ActiveTenant>();
        services.AddSingleton<ITenantMembershipDirectoryReader, AlwaysMemberDirectory>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    static async Task<Result<WorkQueueView>> ReviewWorkAsync(IServiceProvider provider, OperationsFixture source, Uuid actor)
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            new ListWork(source.TenantId, source.ProgramId, "mine", Search: "occurrence_review"),
            new RequestDispatchContext(ProgramManagementServices.Actor(actor), new DirectInvocation()), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result;
    }

    static async Task ProjectAsync(IServiceProvider provider, Uuid tenantId)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        foreach (var directory in services.GetServices<IAccountableWorkItemDirectoryReader>()
                     .DistinctBy(reader => reader.ProjectorName))
        {
            var registration = Assert.Single(services.GetServices<WorkloadRegistration>(), value => value.Name == directory.ProjectorName);
            var projector = (Projector)services.GetRequiredService(registration.ComponentType);
            await new ProjectorScenario(new TenantId(tenantId.ToString())).RunAsync(projector);
            var checkpoint = await directory.LoadCheckpointAsync(tenantId);
            var runner = new ProjectorRunner(services.GetRequiredService<IDomainEventReader>());
            while (true)
            {
                var next = await runner.RunAsync(projector, checkpoint);
                if (next == checkpoint)
                    break;
                checkpoint = next;
            }
        }
    }
}
