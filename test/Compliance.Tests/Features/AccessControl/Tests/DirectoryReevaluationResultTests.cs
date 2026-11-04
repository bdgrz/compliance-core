using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Tests.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class DirectoryReevaluationResultTests
{
    [Fact]
    public async Task ShouldStopStatusFanOutAndReturnFailureGivenFirstEngagementCommandRefused()
    {
        // Arrange
        await using var fixture = await AcceptedStaffFixture.CreateAsync(engagementCount: 2);
        await fixture.ProjectAsync();
        var changed = await fixture.SetStatusAsync(fixture.Lead, false);
        var status = await StatusAsync(fixture, fixture.Lead.StaffMemberId);
        var context = new StatusContext(status);
        var error = new RequestError(RequestErrorKind.Conflict, "The reevaluation source is pending.", isTransient: true);
        var scenario = new ReactorScenario().RespondTo<RecordDirectoryIndependenceReevaluation>(Result.Failure(error));
        var discovery = Discovery(fixture, scenario.Requests);

        // Act
        var result = await discovery.ReconcileStatusAsync(fixture.Tenant, context, changed, CancellationToken.None);

        // Assert
        var failed = Assert.Single(scenario.SentRequests.OfType<RecordDirectoryIndependenceReevaluation>());
        Assert.False(result.IsSuccess);
        Assert.Same(error, result.Error);
        Assert.Equal(fixture.Lead.StaffMemberId, failed.StaffMemberId);

        scenario.RespondTo<RecordDirectoryIndependenceReevaluation>(Result.Success);
        var replay = await discovery.ReconcileStatusAsync(fixture.Tenant, context, changed, CancellationToken.None);
        var retried = scenario.SentRequests.OfType<RecordDirectoryIndependenceReevaluation>().Skip(1).ToArray();
        Assert.True(replay.IsSuccess);
        Assert.Equal(2, retried.Length);
        Assert.Contains(failed, retried);
        Assert.Equal(2, retried.Select(command => command.EngagementId).Distinct().Count());
    }

    [Fact]
    public async Task ShouldStopAcceptanceReconciliationAndReturnFailureGivenFirstStaffCommandRefused()
    {
        // Arrange
        await using var fixture = await AcceptedStaffFixture.CreateAsync(staffCount: 2, engagementCount: 0);
        foreach (var staff in fixture.Staff)
            await fixture.SetStatusAsync(staff, false);
        await fixture.AcceptAsync(fixture.Tenant, fixture.Staff);
        var acceptance = Assert.Single(await fixture.AcceptanceRecordsAsync());
        var context = new RecordReactorContext(acceptance);
        var error = new RequestError(RequestErrorKind.Conflict, "The reevaluation command was refused.");
        var scenario = new ReactorScenario().RespondTo<RecordDirectoryIndependenceReevaluation>(Result.Failure(error));
        var discovery = Discovery(fixture, scenario.Requests);

        // Act
        var result = await discovery.ReconcileAcceptanceAsync(context, acceptance.Event, CancellationToken.None);

        // Assert
        var failed = Assert.Single(scenario.SentRequests.OfType<RecordDirectoryIndependenceReevaluation>());
        Assert.False(result.IsSuccess);
        Assert.Same(error, result.Error);
        Assert.Equal(acceptance.Event.Metadata.EventId, failed.AcceptanceEventId);

        scenario.RespondTo<RecordDirectoryIndependenceReevaluation>(Result.Success);
        var replay = await discovery.ReconcileAcceptanceAsync(context, acceptance.Event, CancellationToken.None);
        var retried = scenario.SentRequests.OfType<RecordDirectoryIndependenceReevaluation>().Skip(1).ToArray();
        Assert.True(replay.IsSuccess);
        Assert.Equal(2, retried.Length);
        Assert.Contains(failed, retried);
        Assert.Equal(2, retried.Select(command => command.StaffMemberId).Distinct().Count());
    }

    static DirectoryReevaluationDiscovery Discovery(AcceptedStaffFixture fixture, IRequestBus bus) => new(
        fixture.Services.GetRequiredService<IDomainEventReader>(),
        fixture.Services.GetRequiredService<IAggregateReader>(),
        fixture.Services.GetRequiredService<FirmStaffViews>(),
        fixture.Services.GetRequiredService<IActualStaffEngagementLocatorReader>(),
        fixture.Services.GetRequiredService<IImmutableContentStore>(), bus);

    static async Task<DomainEventRecord> StatusAsync(AcceptedStaffFixture fixture, Uuid staffMemberId)
    {
        DomainEventRecord? status = null;
        await foreach (var record in fixture.Services.GetRequiredService<IDomainEventReader>()
                           .ReadAsync(new FirmStaffRecord(staffMemberId).Stream, 0, CancellationToken.None))
            if (record.Event is FirmStaffChangeRecorded { ChangeKind: "status" })
                status = record;
        return Assert.IsType<DomainEventRecord>(status);
    }

    sealed class StatusContext(DomainEventRecord source) : RecordReactorContext(source),
        IReactorContext<FirmStaffChangeRecorded>
    {
        public FirmStaffChangeRecorded Trigger => (FirmStaffChangeRecorded)Source.Event;
    }
}
