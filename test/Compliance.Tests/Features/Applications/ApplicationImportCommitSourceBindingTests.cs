using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationImportCommitSourceBindingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRejectChangedNativeSourceTupleGivenAcceptedImportPublication(bool changePosition)
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var command = Assert.IsType<CommitApplicationImportBatch>(await ApplicationImportTargetPreparationTests.SeedAsync(host, "commit"));
        var first = await host.SendAsync(command);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var changed = command with
        {
            Source = changePosition
                ? command.Source with { AggregateVersion = command.Source.AggregateVersion + 1 }
                : command.Source with { EventId = Uuid.CreateVersion4() },
        };

        // Act
        var refused = await host.SendAsync(changed);

        // Assert
        Assert.False(refused.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, refused.Error?.Kind);
        Assert.Single(await host.EventsAsync<ApplicationImportCommitted>(command.Target));
        Assert.IsType<ReceiverOutcome.Applied>(first.Value);
    }
}
