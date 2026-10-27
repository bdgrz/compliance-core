using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Artifacts;
using Bdgrz.Compliance.Features.Evidence;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AuditPopulations;

public sealed partial class GenerateAuditPopulationExportHandlerTests
{
    [Fact]
    public async Task ShouldKeepOriginalArtifactPresentationGivenAuditPopulationExportProcessNativeBoundary()
    {
        // Arrange: real CSV construction, governed artifact registration and retention, with a declared test inspector.
        await using var fixture = new Fixture(ArtifactInspectionState.NotInspected);
        var population = await fixture.SeedPopulationAsync(frozen: true);
        var request = fixture.Request(population, "csv", "original-export-receipt");
        var context = fixture.Context(request);
        var first = await fixture.Handler().HandleAsync(context, CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.Equal(EvidenceArtifactStates.PendingInspection, first.Value.ArtifactState);
        var artifact = await fixture.ArtifactAsync(first.Value.Export.ArtifactId);
        Assert.Null(artifact.RecordInspection(EvidenceInspectionOutcome.Clean, Now.AddHours(2)));
        await fixture.Writer.SaveAsync(artifact, new RequestDispatchContext(RequestActor.System));

        // Act
        var retry = await fixture.Handler().HandleAsync(context, CancellationToken.None);
        var current = await fixture.ArtifactAsync(first.Value.Export.ArtifactId);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(EvidenceArtifactStates.Available, current.State);
        Assert.Equal(first.Value, retry.Value);
        Assert.Equal(1, fixture.Inspector.Calls);
    }

    [Fact]
    public async Task ShouldKeepPopulationRevisionIndependentGivenAuditPopulationExportProcessNativeBoundary()
    {
        // Arrange
        await using var fixture = new Fixture(ArtifactInspectionState.Clean);
        var population = await fixture.SeedPopulationAsync(frozen: true);
        var before = await fixture.PopulationAsync(population);

        // Act
        var result = await fixture.Handler().HandleAsync(fixture.Context(fixture.Request(population, "csv", "independent-export")),
            CancellationToken.None);
        var after = await fixture.PopulationAsync(population);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(before.Revision, after.Revision);
        Assert.Equal(before.Current!.Current!.ContentSha256, after.Current!.Current!.ContentSha256);
    }
}
