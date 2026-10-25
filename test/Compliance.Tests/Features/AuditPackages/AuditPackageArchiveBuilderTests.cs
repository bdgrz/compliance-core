using Bdgrz.Compliance.Features.AuditPackages;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AuditPackages;

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
