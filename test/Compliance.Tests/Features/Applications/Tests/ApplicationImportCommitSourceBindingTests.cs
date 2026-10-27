using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Applications.Tests;

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
