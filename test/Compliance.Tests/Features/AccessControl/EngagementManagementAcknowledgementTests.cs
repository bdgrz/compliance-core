using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using static Bdgrz.Compliance.Tests.Testing.PersonalDecisionFacts;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class EngagementManagementAcknowledgementTests
{
    [Fact]
    public async Task ShouldRetainPersonalAcknowledgementGivenExactDraftAndCompleteServiceFacts()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var (engagement, service) = await SeedEngagementAsync(host);
        var request = await AcknowledgementAsync(host, engagement,
            statement: "I retain responsibility for management decisions and oversight.");

        // Act
        var admitted = await host.HttpAsync(request);
        var process = await RunAsync(host.Provider, host.Tenant, engagement, request.AcknowledgementId);
        var retry = await host.HttpAsync(request);
        var events = await host.EventsAsync<DomainEvent>(
            new EngagementManagementAcknowledgement(host.Tenant, request.AcknowledgementId).Stream);
        var replay = new AggregateScenario<EngagementManagementAcknowledgement>(
            new EngagementManagementAcknowledgement(host.Tenant, request.AcknowledgementId)).Given(events.ToArray())
            .Aggregate;

        // Assert
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);
        Assert.Equal(ProcessState.Completed, process.State);
        Assert.Equal(IndependenceHost.Json(admitted.Value), IndependenceHost.Json(retry.Value));
        var view = await host.Content.ReadJsonAsync<EngagementManagementAcknowledgementView>(host.Tenant.ToString(),
            replay.Acknowledged!.AcknowledgementContent);
        Assert.Equal(host.User, view.UserId);
        Assert.Equal(service, Assert.Single(view.CompleteServiceRecordIds));
        Assert.Equal(request.Statement, view.Statement);
        var acknowledgements = await host.SendAsync(new GetEngagementManagementAcknowledgements(host.Tenant, engagement));
        Assert.Equal(request.AcknowledgementId, Assert.Single(acknowledgements.Value!).AcknowledgementId);
        var current = await host.SendAsync(new GetServiceEngagement(host.Tenant, engagement));
        Assert.False(current.Value!.ProfessionalAccessGranted);
    }

    [Theory]
    [InlineData("revision")]
    [InlineData("facts")]
    [InlineData("epoch")]
    public async Task ShouldRefuseAcknowledgementGivenStaleRevisionFactsOrEpoch(string condition)
    {
        // Arrange
        await using var host = new IndependenceHost();
        var (engagement, _) = await SeedEngagementAsync(host);
        var request = await AcknowledgementAsync(host, engagement);
        request = condition switch
        {
            "revision" => request with { ExpectedEngagementRevision = 2 },
            "facts" => request with { CompleteServiceRecordIds = [Uuid.CreateVersion4()] },
            _ => request with { ExpectedServiceEpoch = 0 },
        };

        // Act
        var result = await host.HttpAsync(request);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(await host.EventsAsync<EngagementPersonalDecisionAdmitted>(
            new ServiceEngagementIndependence(host.Tenant, engagement).Stream));
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("operation")]
    [InlineData("owner")]
    [InlineData("revision")]
    public async Task ShouldRejectMisboundAcknowledgementGivenCorruptedReplay(string corruption)
    {
        // Arrange
        await using var host = new IndependenceHost();
        var (engagement, _) = await SeedEngagementAsync(host);
        var request = await AcknowledgementAsync(host, engagement);
        Assert.True((await host.HttpAsync(request)).IsSuccess);
        await RunAsync(host.Provider, host.Tenant, engagement, request.AcknowledgementId);
        var recorded = Assert.Single(await host.EventsAsync<EngagementManagementAcknowledged>(
            new EngagementManagementAcknowledgement(host.Tenant, request.AcknowledgementId).Stream));
        var corrupted = corruption switch
        {
            "tenant" => recorded with { TenantId = Uuid.CreateVersion4() },
            "operation" => recorded with { OperationId = Uuid.CreateVersion4() },
            "revision" => recorded with { Revision = 2 },
            _ => recorded with { Operation = recorded.Operation with { Owner = new EventStreamAddress("another", "streams", "other") } },
        };

        // Act
        var replay = () => new AggregateScenario<EngagementManagementAcknowledgement>(
            new EngagementManagementAcknowledgement(host.Tenant, request.AcknowledgementId)).Given(corrupted);

        // Assert
        Assert.Throws<InvalidOperationException>(replay);
    }

    [Fact]
    public async Task ShouldRejectOldAcknowledgementIntentGivenNewServiceRecordedAfterDraft()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var (engagement, service) = await SeedEngagementAsync(host);
        var later = Uuid.CreateVersion4();
        Assert.True((await host.SendAsync(new RecordNonattestService(host.Tenant, later, 1,
            IndependenceHost.Service("readiness", new DateOnly(2026, 1, 1))))).IsSuccess);
        var incomplete = new AcknowledgeEngagementManagement(host.Tenant, engagement, Uuid.CreateVersion4(), 1,
            [service], "I retain management responsibility.", 2);

        // Act
        var result = await host.HttpAsync(incomplete);
        var complete = await host.HttpAsync(incomplete with
        {
            AcknowledgementId = Uuid.CreateVersion4(),
            CompleteServiceRecordIds = [service, later],
        });

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(complete.IsSuccess, complete.Error?.Message);
    }
}
