using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Processes;

public sealed class ProcessStageDefinitionTests
{
    [Theory]
    [InlineData("apply", null, false)]
    [InlineData("apply", true, true)]
    [InlineData("reserve", null, true)]
    [InlineData("reserve", false, false)]
    [InlineData("publish", null, false)]
    public void ShouldDispatchPerTargetGivenStageNameOrExplicitOverride(string stage, bool? perTarget, bool expected)
    {
        // Arrange
        var definition = new ProcessStageDefinition(stage, ["Receiver"], null, perTarget);

        // Act
        var result = definition.IsPerTarget;

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task ShouldDispatchOncePerCanonicalTargetGivenApplyStageOverriddenPerTarget()
    {
        // Arrange
        var definition = new ProcessDefinition("OverrideProcess", ["ProbeAdmitted"], true,
            [new ProcessStageDefinition("apply", ["ApplyA"], PerTarget: true)], [],
            (step, _, _) => ValueTask.FromResult<IRequest<ReceiverOutcome>?>(
                new ProbeReceive(step.CommandId, step.Receiver, step.Target)));
        await using var host = new ProcessTestHost(definition: definition with { Name = ProcessTestHost.Process });
        _ = await host.StartAsync("b", "a");

        // Act
        var run = await host.RunAsync();

        // Assert: both targets were dispatched, in canonical order, by one stage.
        Assert.Equal(ProcessState.Completed, run.State);
        Assert.Equal(["a", "b"], host.Recorder.Calls.Select(call => call.Target));
    }
}
