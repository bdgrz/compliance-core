using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Artifacts.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;

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
