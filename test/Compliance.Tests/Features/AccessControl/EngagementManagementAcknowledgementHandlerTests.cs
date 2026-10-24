using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using static Bdgrz.Compliance.Tests.Testing.PersonalDecisionFacts;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class EngagementManagementAcknowledgementHandlerTests
{
    [Theory]
    [InlineData("direct")]
    [InlineData("mcp")]
    [InlineData("system")]
    public async Task ShouldDenyPersonalAcknowledgementGivenNonHttpOrSystemInvocation(string invocation)
    {
        // Arrange
        await using var host = new IndependenceHost();
        var (engagement, _) = await SeedEngagementAsync(host);
        var request = await AcknowledgementAsync(host, engagement);
        RequestInvocation transport = invocation switch
        {
            "mcp" => new McpInvocation("synthetic-tool"),
            "system" => new HttpInvocation("PUT", "/synthetic", "/synthetic", "synthetic"),
            _ => new DirectInvocation(),
        };
        var actor = invocation == "system" ? RequestActor.System : host.Actor;

        // Act
        var result = await host.Bus.DispatchAsync(request, new RequestDispatchContext(actor, transport),
            CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        var owner = await host.HydrateAsync(new ServiceEngagementIndependence(host.Tenant, engagement));
        Assert.Empty(owner.Decisions);
        Assert.Equal(1UL, owner.Revision);
    }

    [Fact]
    public async Task ShouldPersistPersonalDecisionGivenHttpInvocationAndCurrentClientManagementAuthority()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var (engagement, serviceId) = await SeedEngagementAsync(host);
        var request = await AcknowledgementAsync(host, engagement);

        // Act
        var result = await host.HttpAsync(request);
        await RunAsync(host.Provider, host.Tenant, engagement, request.AcknowledgementId);
        var retained = await host.HydrateAsync(new EngagementManagementAcknowledgement(host.Tenant,
            request.AcknowledgementId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(request.AcknowledgementId, result.Value!.OperationId);
        var ack = await host.Content.ReadJsonAsync<EngagementManagementAcknowledgementView>(host.Tenant.ToString(),
            retained.Acknowledged!.AcknowledgementContent);
        Assert.Equal(host.User, ack.UserId);
        Assert.Equal(RbacIds.Member(host.Tenant, host.User).ToString(), ack.Actor.Id);
        Assert.Equal(serviceId, Assert.Single(ack.CompleteServiceRecordIds));
        var history = await host.SendAsync(new GetClientIndependenceHistory(host.Tenant));
        Assert.Equal(serviceId, Assert.Single(history.Value!.Services).ServiceRecordId);
        Assert.Equal(1, history.Value.Sequence);
        var engagementView = await host.SendAsync(new GetServiceEngagement(host.Tenant, engagement));
        Assert.False(engagementView.Value!.ProfessionalAccessGranted);
        Assert.Equal(1, engagementView.Value.Revision);
    }
}
