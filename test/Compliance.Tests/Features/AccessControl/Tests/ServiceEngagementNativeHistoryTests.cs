using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.History;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class ServiceEngagementNativeHistoryTests
{
    [Theory]
    [InlineData("payload")]
    [InlineData("event_id")]
    [InlineData("truncate")]
    public async Task ShouldRejectAlternateIntermediatePrefixGivenCapturedEngagementAuthority(string fault)
    {
        // Arrange
        await using var host = new IndependenceHost();
        var lead = await host.RegisterStaffAsync();
        var id = Uuid.CreateVersion4();
        Assert.True((await host.SendAsync(new CreateServiceEngagement(host.Tenant, id, 0, IndependenceHost.Draft(lead)))).IsSuccess);
        for (var revision = 1; revision <= 2; revision++)
            Assert.True((await host.SendAsync(new AmendServiceEngagement(Uuid.CreateVersion4(), host.Tenant, id,
                revision, IndependenceHost.Draft(lead, "Scope " + revision)))).IsSuccess);
        var owner = await host.HydrateAsync(new ServiceEngagementIndependence(host.Tenant, id));
        var facts = (await host.EventsAsync<DomainEvent>(owner.Stream)).ToArray();
        if (fault == "truncate")
            facts = facts[..^1];
        else
        {
            var original = (ServiceEngagementMutationRecorded)facts[1];
            var changed = fault == "payload" ? original with { Change = original.Change with { ContentId = Uuid.CreateVersion4() } } : original with { };
            changed.AttachMetadata(fault == "event_id" ? original.Metadata with { EventId = Uuid.CreateVersion4() } : original.Metadata);
            facts[1] = changed;
        }

        // Act
        var failure = await Record.ExceptionAsync(async () => _ = await ServiceEngagementHistory.ReadAsync(new Records(owner.Stream, facts), owner));

        // Assert
        if (fault == "truncate")
            Assert.IsType<ImmutableContentUnavailableException>(failure);
        else
            Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.Equal(3UL, owner.Revision);
    }

    [Fact]
    public async Task ShouldRetainFullDecisionHistoryOutsideOwnerGivenManyMaterializedAcknowledgements()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var lead = await host.RegisterStaffAsync();
        var id = Uuid.CreateVersion4();
        Assert.True((await host.SendAsync(new CreateServiceEngagement(host.Tenant, id, 0, IndependenceHost.Draft(lead)))).IsSuccess);
        Uuid first = default;
        for (var index = 0; index < 16; index++)
        {
            var command = await PersonalDecisionFacts.AcknowledgementAsync(host, id);
            if (index == 0)
                first = command.AcknowledgementId;
            Assert.True((await host.HttpAsync(command)).IsSuccess);
            var process = await PersonalDecisionFacts.RunAsync(host.Provider, host.Tenant, id, command.AcknowledgementId);
            Assert.Equal(Bdgrz.Compliance.Features.Processes.Processes.ProcessState.Completed, process.State);
        }

        // Act
        var owner = await host.HydrateAsync(new ServiceEngagementIndependence(host.Tenant, id));
        var history = await ServiceEngagementHistory.ReadAsync(host.Scope.ServiceProvider.GetRequiredService<IDomainEventReader>(), owner);
        var selected = await host.HydrateAsync(new ServiceEngagementIndependence(host.Tenant, id, selectedRootId: first));

        // Assert
        Assert.Equal(16, history.Decisions.Count);
        Assert.All(history.Decisions.Values, decision => Assert.NotNull(decision.Materialized));
        Assert.NotNull(selected.DecisionOf(first)!.Materialized);
        Assert.Null(owner.DecisionOf(first));
        Assert.False(owner.HasPendingPersonalDecision);
        Assert.NotNull(owner.CurrentDecision(EngagementPersonalDecisionKind.ManagementAcknowledgement));
        Assert.Equal(1UL, owner.Revision);
    }

    sealed class Records(EventStreamAddress stream, IReadOnlyList<DomainEvent> facts) : IDomainEventReader
    {
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress source, ulong offset = 0,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            for (var index = offset; source == stream && index < (ulong)facts.Count; index++)
            {
                ct.ThrowIfCancellationRequested();
                yield return new DomainEventRecord(stream, facts[(int)index], index, EventCursor.Start);
            }
            await Task.CompletedTask;
        }
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern source, EventCursor cursor,
            CancellationToken ct = default) => throw new NotSupportedException();
    }
}
