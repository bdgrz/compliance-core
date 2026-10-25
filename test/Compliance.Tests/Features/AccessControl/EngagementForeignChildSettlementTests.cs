using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.SourceUse;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class EngagementForeignChildSettlementTests
{
    [Fact]
    public async Task ShouldRejectForeignMaterializedChildGivenSamePersonalIdentityOnDifferentFirstOwners()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var lead = await host.RegisterStaffAsync();
        var first = Uuid.CreateVersion4();
        var second = Uuid.CreateVersion4();
        foreach (var id in new[] { first, second })
            Assert.True((await host.SendAsync(new CreateServiceEngagement(host.Tenant, id, 0, IndependenceHost.Draft(lead)))).IsSuccess);
        var command = await PersonalDecisionFacts.AcknowledgementAsync(host, first);
        Assert.True((await host.HttpAsync(command)).IsSuccess);
        var completed = await PersonalDecisionFacts.RunAsync(host.Provider, host.Tenant, first, command.AcknowledgementId);
        Assert.Equal(Bdgrz.Compliance.Features.Processes.ProcessState.Completed, completed.State);
        var child = await host.HydrateAsync(new EngagementManagementAcknowledgement(host.Tenant, command.AcknowledgementId));
        var secondCommand = command with { EngagementId = second };
        Assert.True((await host.HttpAsync(secondCommand)).IsSuccess);
        _ = await PersonalDecisionFacts.RunAsync(host.Provider, host.Tenant, second, command.AcknowledgementId);
        var pending = await host.HydrateAsync(new ServiceEngagementIndependence(host.Tenant, second));
        var decision = pending.DecisionOf(command.AcknowledgementId)!;
        Assert.NotNull(decision.Authorized);
        Assert.True(decision.IsPending);
        var before = (await host.EventsAsync<DomainEvent>(pending.Stream)).Count;
        var foreign = new SettleEngagementPersonalDecision(Uuid.CreateVersion4(), NativeFactReference.Of(child.Stream, child.Acknowledged!),
            pending.Stream, command.AcknowledgementId, 1, null, decision.Admitted.Intent, second);

        // Act
        var refused = await PersonalDecisionFacts.SendReceiverAsync(host.Provider, foreign);

        // Assert
        Assert.False(refused.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, refused.Error?.Kind);
        Assert.False(refused.Error!.IsTransient);
        Assert.Equal(before, (await host.EventsAsync<DomainEvent>(pending.Stream)).Count);
    }
}
