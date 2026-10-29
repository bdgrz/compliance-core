using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Changes.Services;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Changes.Aggregates;
using Bdgrz.Compliance.Features.Changes.Commands;
using Bdgrz.Compliance.Features.Changes.Events;
using Bdgrz.Compliance.Features.Changes.Reactors;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Tests.Features.Changes.Tests;

public sealed class ComplianceImpactReactorTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 19, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldAdvanceCheckpointGivenSupersededSourceEventAnswer()
    {
        // Arrange
        var content = ChangeSeeds.Content();
        var trigger = await AssessedTriggerAsync(content);
        var scenario = new ReactorScenario(new TenantId(trigger.TenantId.ToString())).Given(trigger);
        TypedReactionBus.FailWith<LinkImpactReviewWork>(scenario, ImpactReactionOutcomes.Superseded);
        var checkpoints = new InMemoryProjectionCheckpointStore();
        var reactor = new LinkImpactReviewWorkReactor(checkpoints, TypedReactionBus.For(scenario), content);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        await scenario.RunAsync(reactor, deadline.Token);
        var after = await checkpoints.LoadAsync(new CheckpointIdentity(reactor.Name, reactor.Pattern));

        // Assert
        Assert.Single(TypedReactionBus.Sent(scenario).OfType<LinkImpactReviewWork>());
        Assert.NotEqual(ProjectionCheckpoint.Start, after);
    }

    [Fact]
    public async Task ShouldKeepCheckpointAndReplayGivenTransientDependencyLag()
    {
        // Arrange
        var content = ChangeSeeds.Content();
        var trigger = await AssessedTriggerAsync(content);
        var scenario = new ReactorScenario(new TenantId(trigger.TenantId.ToString())).Given(trigger);
        TypedReactionBus.FailWith<LinkImpactReviewWork>(scenario, ImpactReactionOutcomes.NotReady("The review work is not visible yet."));
        var checkpoints = new InMemoryProjectionCheckpointStore();
        var reactor = new LinkImpactReviewWorkReactor(checkpoints, TypedReactionBus.For(scenario), content);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        var failure = await Assert.ThrowsAsync<ReactionCommandFailedException>(
            () => scenario.RunAsync(reactor, deadline.Token));
        var identity = new CheckpointIdentity(reactor.Name, reactor.Pattern);
        var before = await checkpoints.LoadAsync(identity, deadline.Token);
        TypedReactionBus.Succeed<LinkImpactReviewWork>(scenario);
        await scenario.RunAsync(reactor, deadline.Token);
        var afterRecovery = await checkpoints.LoadAsync(identity);

        // Assert
        Assert.True(failure.Error.IsTransient);
        Assert.Equal(ProjectionCheckpoint.Start, before);
        Assert.NotEqual(ProjectionCheckpoint.Start, afterRecovery);
        Assert.Equal(2, TypedReactionBus.Sent(scenario).OfType<LinkImpactReviewWork>().Count());
    }

    [Fact]
    public void ShouldDistinguishSupersessionFromLagGivenDecisionAgainstRetainedAssessments()
    {
        // Arrange
        var assessedAt = Now.AddHours(-2);

        // Act
        var current = ImpactReactionOutcomes.ClassifyDecision(2, 2, true, false, Now, assessedAt);
        var replaced = ImpactReactionOutcomes.ClassifyDecision(1, 2, true, false, Now, assessedAt);
        var decided = ImpactReactionOutcomes.ClassifyDecision(2, 2, true, true, Now, assessedAt);
        var older = ImpactReactionOutcomes.ClassifyDecision(2, 2, true, false, assessedAt.AddMinutes(-1), assessedAt);
        var lag = ImpactReactionOutcomes.ClassifyDecision(3, 2, false, false, Now, assessedAt);

        // Assert
        Assert.Null(current);
        Assert.Equal(ImpactReactionOutcomes.Superseded, replaced);
        Assert.Equal(ImpactReactionOutcomes.Superseded, decided);
        Assert.Equal(ImpactReactionOutcomes.Superseded, older);
        Assert.True(lag!.IsTransient);
        Assert.False(ImpactReactionOutcomes.Superseded.IsTransient);
    }

    static async ValueTask<SignificantChangeImpactAssessed> AssessedTriggerAsync(IImmutableContentStore content)
    {
        var tenantId = Uuid.CreateVersion4();
        var change = ComplianceImpactReactionHandlerTests.AssessedChange(tenantId, Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), RbacIds.Member(tenantId, Uuid.CreateVersion4()), Now, [Uuid.CreateVersion4()]);
        var owner = await ChangeSeeds.OwnerAsync(content, change);
        return new AggregateScenario<SignificantChange>(owner).PendingEvents.OfType<SignificantChangeImpactAssessed>()
            .Single();
    }
}
