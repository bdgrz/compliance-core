using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.Applications.Handlers;
using Bdgrz.Compliance.Features.Applications.Processes;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationImportExecutionPreparationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReplayUnitOrRequestedCancellationGivenAuthorizationWinsBeforeLosingPlanReadFailure(bool cancel)
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var (batch, process, _) = await ApplicationImportReceiverPreparationTests.SealedAsync(host);
        var claim = (await host.StepAsync(process, "claim", nameof(ClaimApplicationImportSource), process.Targets[0]))!;
        var claimed = await host.SendAsync(claim);
        Assert.True(claimed.IsSuccess, claimed.Error?.Message);
        var command = new ExecuteApplicationImport(ApplicationImportExecutionProcess.ExecuteCommandId(process), host.TenantId, batch.Id);
        var context = ApplicationImportReceiverPreparationTests.Context(command);
        var healthy = Handler(host, host.Content);
        using var cancellation = new CancellationTokenSource();
        var winner = Result.Failure(new RequestError(RequestErrorKind.Conflict, "No winner yet."));
        var fault = new ApplicationImportReceiverPreparationTests.FaultStore(host.Content, async () =>
        {
            winner = await healthy.HandleAsync(context, CancellationToken.None);
            Assert.True(winner.IsSuccess, winner.Error?.Message);
            if (cancel)
                cancellation.Cancel();
        });
        var result = Result.Failure(new RequestError(RequestErrorKind.Conflict, "Not executed yet."));

        // Act
        var failure = await Record.ExceptionAsync(async () => result = await Handler(host, fault)
            .HandleAsync(context, cancellation.Token));
        var authorizations = await host.EventsAsync<ApplicationImportExecutionAuthorized>(batch.Stream);

        // Assert
        Assert.True(winner.IsSuccess);
        Assert.Single(authorizations);
        Assert.Equal(command.CommandId, authorizations[0].Operation.LogicalId);
        if (cancel)
            Assert.Equal(cancellation.Token, Assert.IsAssignableFrom<OperationCanceledException>(failure).CancellationToken);
        else
        {
            Assert.Null(failure);
            Assert.True(result.IsSuccess, result.Error?.Message);
        }
        Assert.Equal(1, fault.Reads);
    }

    [Fact]
    public async Task ShouldReplayAuthorizedUnitWithoutPlanBodyGivenCurrentSourceClaimAndTenant()
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var (batch, process, _) = await ApplicationImportReceiverPreparationTests.SealedAsync(host);
        var claim = (await host.StepAsync(process, "claim", nameof(ClaimApplicationImportSource), process.Targets[0]))!;
        var claimed = await host.SendAsync(claim);
        Assert.True(claimed.IsSuccess, claimed.Error?.Message);
        var command = new ExecuteApplicationImport(ApplicationImportExecutionProcess.ExecuteCommandId(process), host.TenantId, batch.Id);
        var first = await Handler(host, host.Content).HandleAsync(ApplicationImportReceiverPreparationTests.Context(command), CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var fault = new ApplicationImportReceiverPreparationTests.FaultStore(host.Content, null);

        // Act
        var replay = await Handler(host, fault).HandleAsync(ApplicationImportReceiverPreparationTests.Context(command), CancellationToken.None);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(0, fault.Reads);
        Assert.Single(await host.EventsAsync<ApplicationImportExecutionAuthorized>(batch.Stream));
    }

    static ExecuteApplicationImportHandler Handler(ApplicationImportHost host, IImmutableContentStore content) =>
        new(host.Reader, host.Services.GetRequiredService<IAggregateExecutor>(),
            new ApplicationImportVerifier(host.Reader, content), host.Activity, host.Clock);
}
