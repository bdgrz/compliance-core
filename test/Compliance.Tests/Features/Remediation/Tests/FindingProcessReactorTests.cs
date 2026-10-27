using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
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
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
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
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.Remediation.Reactors;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Remediation.Tests;

/// <summary>The root reactors turn committed admission and closure facts into durable process records.</summary>
public sealed class FindingProcessReactorTests
{
    [Fact]
    public async Task ShouldMaterializeActionExactlyOnceGivenCommittedAdmissionDeliveredTwice()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var raised = await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        var finding = await fixture.AddActionAsync(raised, fixture.OwnerMemberId, fixture.Today.AddDays(5), "Fix.",
            materialize: false);
        var actionId = finding.CorrectiveActions[0].ActionId;
        var stream = new FindingAggregate(fixture.TenantId, fixture.ProgramId, finding.FindingId).Stream;
        var record = await AdmissionRecordAsync(fixture, stream);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var reactor = new CorrectiveActionMaterializationReactor(new InMemoryProjectionCheckpointStore(),
            scope.ServiceProvider.GetRequiredService<ProcessRootReaction>(),
            scope.ServiceProvider.GetRequiredService<Bdgrz.Compliance.Features.Content.Contracts.IImmutableContentStore>(),
            scope.ServiceProvider.GetRequiredService<ISourceFactResolver>());
        await new ReactorScenario(new TenantId(fixture.TenantId.ToString())).RunAsync(reactor);

        // Act
        await reactor.HandleAsync(AdministratorTransitionHost.ReactionOf<CorrectiveActionAdmitted>(record),
            CancellationToken.None);
        var materialized = await fixture.ActionAsync(finding.FindingId, actionId);
        await reactor.HandleAsync(AdministratorTransitionHost.ReactionOf<CorrectiveActionAdmitted>(record),
            CancellationToken.None);
        var again = await fixture.ActionAsync(finding.FindingId, actionId);
        var process = await scope.ServiceProvider.GetRequiredService<IProcessRecordStore>()
            .ReadAsync(await FindingProcesses.MaterializationKey(fixture.Provider, fixture.TenantId, actionId));

        // Assert
        Assert.True(materialized.IsMaterialized);
        Assert.Equal(1, materialized.Revision);
        Assert.Equal(materialized.Version, again.Version);
        Assert.Equal(ProcessState.Completed, process!.State);
        Assert.Equal(record.Event.Metadata.EventId, process.Source.EventId);
    }

    [Fact]
    public async Task ShouldCloseFindingExactlyOnceGivenCommittedClosureAcceptanceDeliveredTwice()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await FindingTests.RaiseWithCompletedActionAsync(fixture);
        var close = fixture.Close(finding);
        await PersonalReadinessClosureTransportTests.CloseHttpAsync(fixture.Provider, fixture.ApproverUserId, close);
        var stream = new FindingAggregate(fixture.TenantId, fixture.ProgramId, finding.FindingId).Stream;
        DomainEventRecord? record = null;
        await foreach (var candidate in fixture.Provider.GetRequiredService<IDomainEventReader>().ReadAsync(stream, 0))
            if (candidate.Event is FindingClosureAccepted)
                record = candidate;
        await using var scope = fixture.Provider.CreateAsyncScope();
        var reactor = new FindingClosureReactor(new InMemoryProjectionCheckpointStore(),
            scope.ServiceProvider.GetRequiredService<ProcessRootReaction>(),
            scope.ServiceProvider.GetRequiredService<Bdgrz.Compliance.Features.Content.Contracts.IImmutableContentStore>(),
            scope.ServiceProvider.GetRequiredService<ISourceFactResolver>());
        await new ReactorScenario(new TenantId(fixture.TenantId.ToString())).RunAsync(reactor);

        // Act
        await reactor.HandleAsync(AdministratorTransitionHost.ReactionOf<FindingClosureAccepted>(record!),
            CancellationToken.None);
        var closedOnce = await fixture.FindingStreamAsync(finding.FindingId);
        await reactor.HandleAsync(AdministratorTransitionHost.ReactionOf<FindingClosureAccepted>(record!),
            CancellationToken.None);
        var closedTwice = await fixture.FindingStreamAsync(finding.FindingId);
        var process = await scope.ServiceProvider.GetRequiredService<IProcessRecordStore>()
            .ReadAsync(await fixture.ClosureKey(close.CommandId));

        // Assert
        Assert.NotNull(closedOnce.Closed);
        Assert.Equal(closedOnce.Version, closedTwice.Version);
        Assert.Single((await fixture.FindingHistoryAsync(finding.FindingId)).ClosureHistory);
        Assert.Equal(ProcessState.Completed, process!.State);
        Assert.Single(process.Targets);
    }

    static async Task<DomainEventRecord> AdmissionRecordAsync(OperationsFixture fixture, EventStreamAddress stream)
    {
        await foreach (var candidate in fixture.Provider.GetRequiredService<IDomainEventReader>().ReadAsync(stream, 0))
            if (candidate.Event is CorrectiveActionAdmitted)
                return candidate;
        throw new InvalidOperationException("The admission was not committed.");
    }
}
