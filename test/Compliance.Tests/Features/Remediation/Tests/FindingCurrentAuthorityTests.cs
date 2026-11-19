using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Handlers;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.Remediation.Tests;

public sealed class FindingCurrentAuthorityTests
{
    [Fact]
    public async Task ShouldRefuseAcceptedProposalRetryGivenProposerSuspended()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var finding = await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        var artifact = await ProposeFindingClosureHandlerTests.ArtifactAsync(fixture,
            Bdgrz.Compliance.Features.Evidence.Models.EvidenceInspectionOutcome.Clean);
        var request = ProposeFindingClosureHandlerTests.Command(fixture, finding, artifact);
        await using var scope = provider.CreateAsyncScope();
        var handler = ActivatorUtilities.CreateInstance<ProposeFindingClosureHandler>(scope.ServiceProvider);
        Assert.True((await handler.HandleAsync(ProposeFindingClosureHandlerTests.Context(request, fixture.OwnerUserId), CancellationToken.None)).IsSuccess);
        await ProgramManagementServices.SeedAsync(provider, new Member(fixture.TenantId, fixture.OwnerUserId),
            member => member.Suspend(fixture.LeadMemberId, "Lead", DateTimeOffset.UtcNow, "Access ended."));
        var before = await fixture.FindingStreamAsync(finding.FindingId);

        // Act
        var retry = await handler.HandleAsync(ProposeFindingClosureHandlerTests.Context(request, fixture.OwnerUserId), CancellationToken.None);
        var after = await fixture.FindingStreamAsync(finding.FindingId);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, retry.Error?.Kind);
        Assert.Equal(before.Version, after.Version);
    }

    [Fact]
    public async Task ShouldRefuseReopenGivenActorHasNoLeadReopenPermission()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var finding = await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        var request = new ReopenFinding(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, finding.FindingId,
            finding.Revision, "Finding needs further work.");
        await using var scope = provider.CreateAsyncScope();
        var handler = ActivatorUtilities.CreateInstance<ReopenFindingHandler>(scope.ServiceProvider);
        var before = await fixture.FindingStreamAsync(finding.FindingId);

        // Act
        var result = await handler.HandleAsync(ProposeFindingClosureHandlerTests.Context(request, fixture.OwnerUserId), CancellationToken.None);
        var after = await fixture.FindingStreamAsync(finding.FindingId);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(before.Version, after.Version);
    }
}
