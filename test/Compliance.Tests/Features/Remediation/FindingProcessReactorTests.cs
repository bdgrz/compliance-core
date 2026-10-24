using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Features.Readiness;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Remediation;

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
            scope.ServiceProvider.GetRequiredService<ProcessRootReaction>());

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
            scope.ServiceProvider.GetRequiredService<Bdgrz.Compliance.Features.Content.IImmutableContentStore>());

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
        Assert.Single(closedTwice.ClosureHistory);
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
