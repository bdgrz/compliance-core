using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.Remediation.Reactors;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.Remediation.Tests;

public sealed class CorrectiveActionOutcomeReactorTests
{
    [Theory]
    [InlineData("completed")]
    [InlineData("cancelled")]
    public async Task ShouldRepeatStableReportGivenSameCommittedTerminalSource(string outcome)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var (finding, source) = await FindingCorrectiveActionOutcomeWorkflowTests.TerminalAsync(fixture, outcome);
        var scenario = new ReactorScenario(new TenantId(fixture.TenantId.ToString()));
        await using var scope = provider.CreateAsyncScope();
        var reactor = new CorrectiveActionOutcomeReactor(new InMemoryProjectionCheckpointStore(), scenario.Requests,
            scope.ServiceProvider.GetRequiredService<ISourceFactResolver>(), fixture.Content);
        await scenario.RunAsync(reactor);

        // Act
        var first = outcome == "completed"
            ? await reactor.HandleAsync(AdministratorTransitionHost.ReactionOf<CorrectiveActionCompletedV2>(source), CancellationToken.None)
            : await reactor.HandleAsync(AdministratorTransitionHost.ReactionOf<CorrectiveActionCancelled>(source), CancellationToken.None);
        var second = outcome == "completed"
            ? await reactor.HandleAsync(AdministratorTransitionHost.ReactionOf<CorrectiveActionCompletedV2>(source), CancellationToken.None)
            : await reactor.HandleAsync(AdministratorTransitionHost.ReactionOf<CorrectiveActionCancelled>(source), CancellationToken.None);

        // Assert
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(2, scenario.SentRequests.Count);
        var expected = FindingCorrectiveActionOutcomeWorkflowTests.Report(fixture, finding, source, outcome);
        Assert.All(scenario.SentRequests, request => Assert.Equal(expected, Assert.IsType<RecordCorrectiveActionOutcome>(request)));
    }

    [Fact]
    public async Task ShouldReturnRefusalGivenOutcomeReceiverRefusesReport()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var (_, source) = await FindingCorrectiveActionOutcomeWorkflowTests.TerminalAsync(fixture, "completed");
        var error = new RequestError(RequestErrorKind.Conflict, "Finding append was refused.", true);
        var scenario = new ReactorScenario(new TenantId(fixture.TenantId.ToString()))
            .RespondTo<RecordCorrectiveActionOutcome>(Result.Failure(error));
        await using var scope = provider.CreateAsyncScope();
        var reactor = new CorrectiveActionOutcomeReactor(new InMemoryProjectionCheckpointStore(), scenario.Requests,
            scope.ServiceProvider.GetRequiredService<ISourceFactResolver>(), fixture.Content);
        await scenario.RunAsync(reactor);

        // Act
        var result = await reactor.HandleAsync(AdministratorTransitionHost.ReactionOf<CorrectiveActionCompletedV2>(source),
            CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(error, result.Error);
        Assert.Single(scenario.SentRequests);
    }
}
