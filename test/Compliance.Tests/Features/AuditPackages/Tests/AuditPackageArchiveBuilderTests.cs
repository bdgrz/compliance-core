using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Services;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
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
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;

public sealed class AuditPackageArchiveBuilderTests
{
    [Fact]
    public async Task ShouldBuildIdenticalArchiveGivenSamePinnedBytesInDifferentManifestOrder()
    {
        // Arrange
        await using var fixture = new RecordAuditPackageBuildResultHandlerTests.Fixture();
        var command = await fixture.StartBuildAsync();
        var source = await new AuditPackageSourceVerifier(fixture.Inner.Reader, fixture.Inner.ContentStore, fixture.Inner.PeriodEvents).BuildAsync(
            fixture.Inner.TenantId, fixture.PackageId, command.BuildId, 1, command.OperationId, command.Source,
            command.Intent, CancellationToken.None);
        var manifest = source.Value!.Manifest;
        var builder = new AuditPackageArchiveBuilder(fixture.Inner.ArtifactStore);

        // Act
        var first = await builder.BuildAsync(fixture.Inner.TenantId, manifest, CancellationToken.None);
        var replay = await builder.BuildAsync(fixture.Inner.TenantId,
            manifest with { Entries = manifest.Entries.Reverse().ToArray() }, CancellationToken.None);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(first.Value!.ArchiveSha256, replay.Value!.ArchiveSha256);
        Assert.Equal(first.Value.ArchiveBytes, replay.Value.ArchiveBytes);
        Assert.Equal(first.Value.ArchiveArtifactId, replay.Value.ArchiveArtifactId);
    }

    [Fact]
    public async Task ShouldKeepMissingSourcePendingGivenIdenticalDigestInAnotherTenant()
    {
        // Arrange
        await using var fixture = new RecordAuditPackageBuildResultHandlerTests.Fixture();
        var command = await fixture.StartBuildAsync();
        var source = await new AuditPackageSourceVerifier(fixture.Inner.Reader, fixture.Inner.ContentStore, fixture.Inner.PeriodEvents).BuildAsync(
            fixture.Inner.TenantId, fixture.PackageId, command.BuildId, 1, command.OperationId, command.Source,
            command.Intent, CancellationToken.None);
        var otherTenant = Uuid.CreateVersion4();

        // Act
        var result = await new AuditPackageArchiveBuilder(fixture.Inner.ArtifactStore).BuildAsync(otherTenant,
            source.Value!.Manifest with { TenantId = otherTenant }, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(result.Error!.IsTransient);
    }

    [Fact]
    public async Task ShouldRefusePathTraversalGivenPinnedSourceBytes()
    {
        // Arrange
        await using var fixture = new RecordAuditPackageBuildResultHandlerTests.Fixture();
        var command = await fixture.StartBuildAsync();
        var source = await new AuditPackageSourceVerifier(fixture.Inner.Reader, fixture.Inner.ContentStore, fixture.Inner.PeriodEvents).BuildAsync(
            fixture.Inner.TenantId, fixture.PackageId, command.BuildId, 1, command.OperationId, command.Source,
            command.Intent, CancellationToken.None);
        var manifest = source.Value!.Manifest;

        // Act
        var result = await new AuditPackageArchiveBuilder(fixture.Inner.ArtifactStore).BuildAsync(fixture.Inner.TenantId,
            manifest with { Entries = [manifest.Entries[0] with { RelativePath = "../foreign.json" }] }, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.False(result.Error!.IsTransient);
    }
}
