using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using static Bdgrz.Compliance.Tests.Features.AuditPackages.Tests.AmendAuditPackageHandlerTests;
using AuditPackageAggregate = Bdgrz.Compliance.Features.AuditPackages.Rules.AuditPackageRules;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
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
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AuditPackages.Commands;
using Bdgrz.Compliance.Features.AuditPackages.Handlers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;

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
            new AuditPackageSourceBuilder(fixture.Examinations, fixture.Periods), fixture.ArtifactStore, fixture.ContentStore);

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
            new AuditPackageSourceBuilder(fixture.Examinations, fixture.Periods), fixture.ArtifactStore, fixture.ContentStore);

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
            new AuditPackageSourceBuilder(fixture.Examinations, fixture.Periods), fixture.ArtifactStore, fixture.ContentStore);

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
            new AuditPackageSourceBuilder(fixture.Examinations, fixture.Periods), fixture.ArtifactStore, fixture.ContentStore);

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
            new AuditPackageSourceBuilder(fixture.Examinations, fixture.Periods), fixture.ArtifactStore, fixture.ContentStore);

        // Act
        var result = await handler.HandleAsync(Fixture.SystemContext(command),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
    }
}
