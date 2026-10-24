using System.Security.Claims;
using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AdvisorCollaboration;

public sealed class AdvisorHandoffQueryHandlerTests
{
    [Fact]
    public async Task ShouldReturnSelectedExactVersionsAndLifecycleGivenScopedHandoff()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var handoffId = Uuid.CreateVersion4();
        var record = AdvisorHost.NewSource();
        Assert.True((await host.CreateAsync(handoffId, [record], "Type I readiness review")).IsSuccess);
        host.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True((await host.ReadyAsync(handoffId, 1)).IsSuccess);

        // Act
        var result = await new GetAdvisorRequestHandler(host.Handoffs).HandleAsync(
            new RequestContext<GetAdvisorRequest>(new GetAdvisorRequest(host.TenantId, host.ProgramId, handoffId),
                new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(host.TenantId, result.Value.TenantId);
        Assert.Equal(host.ProgramId, result.Value.ProgramId);
        Assert.Equal(handoffId, result.Value.AdvisorRequestId);
        Assert.Equal("Type I readiness review", result.Value.Purpose);
        Assert.Equal(AdvisorHost.Expiry, result.Value.ExpiresOn);
        Assert.Equal("ready_for_review", result.Value.Status);
        Assert.Equal(2, result.Value.Revision);
        Assert.Equal(AdvisorHost.Start, result.Value.CreatedAt);
        Assert.Equal(AdvisorHost.Start.AddMinutes(1), result.Value.UpdatedAt);
        Assert.Equal(host.Lead.Id, result.Value.CreatedBy.Id);
        Assert.Equal([record], result.Value.RecordVersions);
    }

    [Fact]
    public async Task ShouldReturnComparisonLineageGivenRepeatedHandoff()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (priorHandoffId, source) = await host.SeedReadyAsync();
        Assert.True((await host.EndAsync(priorHandoffId, 2, revoke: false)).IsSuccess);
        var handoffId = Uuid.CreateVersion4();
        Assert.True((await host.CreateAsync(handoffId, [new AdvisorRecordVersion(source.RecordId, Uuid.CreateVersion4())],
            "Follow-up review", priorHandoffId)).IsSuccess);

        // Act
        var result = await new GetAdvisorRequestHandler(host.Handoffs).HandleAsync(
            new RequestContext<GetAdvisorRequest>(new GetAdvisorRequest(host.TenantId, host.ProgramId, handoffId),
                new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(priorHandoffId, result.Value.ComparedToHandoffId);
    }

    [Fact]
    public async Task ShouldNotDiscloseHandoffGivenDifferentProgramScope()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, _) = await host.SeedReadyAsync();

        // Act
        var result = await new GetAdvisorRequestHandler(host.Handoffs).HandleAsync(
            new RequestContext<GetAdvisorRequest>(new GetAdvisorRequest(host.TenantId, Uuid.CreateVersion4(), handoffId),
                new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldReturnNotFoundGivenMissingHandoff()
    {
        // Arrange
        await using var host = new AdvisorHost();

        // Act
        var result = await new GetAdvisorRequestHandler(host.Handoffs).HandleAsync(
            new RequestContext<GetAdvisorRequest>(new GetAdvisorRequest(host.TenantId, host.ProgramId,
                Uuid.CreateVersion4()), new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
    }
}
