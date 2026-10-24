using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Bdgrz.Compliance.Features.AuditPackages;
using Bdgrz.Compliance.Features.Artifacts;
using Cntryl.Portia;
using static Bdgrz.Compliance.Tests.Features.AuditPackages.AmendAuditPackageHandlerTests;
using AuditPackageAggregate = Bdgrz.Compliance.Features.AuditPackages.AuditPackageRules;

namespace Bdgrz.Compliance.Tests.Features.AuditPackages;

public sealed class ValidateAuditPackageHandlerTests
{
    [Fact]
    public async Task ShouldPassExactFrozenTypeISourceSelectionGivenBuiltPackage()
    {
        // Arrange
        await using var fixture = new Fixture();
        var baseline = await fixture.SeedBaselineAsync();
        var packageId = Uuid.CreateVersion4();
        await fixture.SeedBuiltPackageAsync(packageId, baseline);
        var command = new ValidateAuditPackage(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, packageId, 1, await fixture.ManifestRevisionAsync(packageId), "validate-exact-type-i");
        var handler = new ValidateAuditPackageHandler(fixture.RootOperation,
            new AuditPackageSourceBuilder(fixture.Reader, fixture.Periods), fixture.ArtifactStore, fixture.ContentStore);

        // Act
        var result = await handler.HandleAsync(Fixture.Context(fixture.UserId, command),
            CancellationToken.None);
        var saved = await fixture.ReadPackageAsync(packageId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var version = Assert.Single(saved.Current!.Versions);
        Assert.Equal("validated", version.Status);
        var validation = Assert.Single(version.Validations);
        Assert.Equal("passed", validation.Status);
        Assert.Empty(validation.Findings);
    }

    [Fact]
    public async Task ShouldPersistNonOverrideableFindingGivenSelectionOutsideFrozenBaseline()
    {
        // Arrange
        await using var fixture = new Fixture();
        var baseline = await fixture.SeedBaselineAsync();
        var packageId = Uuid.CreateVersion4();
        await fixture.SeedBuiltPackageAsync(packageId, baseline, firstSourceHash: new string('d', 64));
        var command = new ValidateAuditPackage(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, packageId, 1, await fixture.ManifestRevisionAsync(packageId), "validate-source-mismatch");
        var handler = new ValidateAuditPackageHandler(fixture.RootOperation,
            new AuditPackageSourceBuilder(fixture.Reader, fixture.Periods), fixture.ArtifactStore, fixture.ContentStore);

        // Act
        var result = await handler.HandleAsync(Fixture.Context(fixture.UserId, command),
            CancellationToken.None);
        var saved = await fixture.ReadPackageAsync(packageId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var version = Assert.Single(saved.Current!.Versions);
        Assert.Equal("validation_blocked", version.Status);
        var validation = Assert.Single(version.Validations);
        Assert.Equal("blocked", validation.Status);
        var finding = Assert.Single(validation.Findings);
        Assert.Equal("frozen_source_mismatch", finding.Code);
        Assert.True(finding.BlocksDelivery);
        Assert.False(finding.OverrideAllowed);
    }

    [Fact]
    public async Task ShouldBlockUnavailableArchiveGivenExactFrozenSourceSelection()
    {
        // Arrange
        await using var fixture = new Fixture();
        var baseline = await fixture.SeedBaselineAsync();
        var packageId = Uuid.CreateVersion4();
        await fixture.SeedBuiltPackageAsync(packageId, baseline, storeArchive: false);
        var command = new ValidateAuditPackage(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, packageId, 1, await fixture.ManifestRevisionAsync(packageId), "validate-missing-archive");
        var handler = new ValidateAuditPackageHandler(fixture.RootOperation,
            new AuditPackageSourceBuilder(fixture.Reader, fixture.Periods), fixture.ArtifactStore, fixture.ContentStore);

        // Act
        var result = await handler.HandleAsync(Fixture.Context(fixture.UserId, command),
            CancellationToken.None);
        var saved = await fixture.ReadPackageAsync(packageId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var validation = Assert.Single(saved.Current!.Versions[0].Validations);
        Assert.Equal("blocked", validation.Status);
        Assert.Equal("archive_content_unavailable", Assert.Single(validation.Findings).Code);
    }

    [Fact]
    public async Task ShouldBlockArchiveEntryChangedAfterBuildGivenExactFrozenSourceSelection()
    {
        // Arrange
        await using var fixture = new Fixture();
        var baseline = await fixture.SeedBaselineAsync();
        var packageId = Uuid.CreateVersion4();
        await fixture.SeedBuiltPackageAsync(packageId, baseline, corruptArchiveEntry: true);
        var command = new ValidateAuditPackage(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, packageId, 1, await fixture.ManifestRevisionAsync(packageId), "validate-tampered-archive-entry");
        var handler = new ValidateAuditPackageHandler(fixture.RootOperation,
            new AuditPackageSourceBuilder(fixture.Reader, fixture.Periods), fixture.ArtifactStore, fixture.ContentStore);

        // Act
        var result = await handler.HandleAsync(Fixture.Context(fixture.UserId, command),
            CancellationToken.None);
        var saved = await fixture.ReadPackageAsync(packageId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var validation = Assert.Single(saved.Current!.Versions[0].Validations);
        Assert.Equal("blocked", validation.Status);
        Assert.Equal("archive_content_mismatch", Assert.Single(validation.Findings).Code);
    }

    [Fact]
    public async Task ShouldRejectSystemActorGivenPackageValidation()
    {
        // Arrange
        await using var fixture = new Fixture();
        var command = new ValidateAuditPackage(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, Uuid.CreateVersion4(), 1, await fixture.ManifestRevisionAsync(Uuid.CreateVersion4()), "validate-system-actor");
        var handler = new ValidateAuditPackageHandler(fixture.RootOperation,
            new AuditPackageSourceBuilder(fixture.Reader, fixture.Periods), fixture.ArtifactStore, fixture.ContentStore);

        // Act
        var result = await handler.HandleAsync(Fixture.SystemContext(command),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
    }
}
