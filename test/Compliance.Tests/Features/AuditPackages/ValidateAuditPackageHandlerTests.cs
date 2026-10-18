using Bdgrz.Compliance.Features.AuditPackages;
using Cntryl.Portia;
using static Bdgrz.Compliance.Tests.Features.AuditPackages.AmendAuditPackageHandlerTests;
using AuditPackageAggregate = Bdgrz.Compliance.Features.AuditPackages.AuditPackage;

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
        var command = new ValidateAuditPackage(fixture.TenantId, fixture.ProgramId, packageId,
            1, "validate-exact-type-i");
        var handler = new ValidateAuditPackageHandler(fixture.Executor, fixture.Reader,
            new AuditPackageSourceBuilder(fixture.Reader), TimeProvider.System);

        // Act
        var result = await handler.HandleAsync(Fixture.Context(fixture.UserId, command),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AuditPackageAggregate(fixture.TenantId,
            fixture.ProgramId, packageId));

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
        await fixture.SeedBuiltPackageAsync(packageId, baseline, new string('d', 64));
        var command = new ValidateAuditPackage(fixture.TenantId, fixture.ProgramId, packageId,
            1, "validate-source-mismatch");
        var handler = new ValidateAuditPackageHandler(fixture.Executor, fixture.Reader,
            new AuditPackageSourceBuilder(fixture.Reader), TimeProvider.System);

        // Act
        var result = await handler.HandleAsync(Fixture.Context(fixture.UserId, command),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AuditPackageAggregate(fixture.TenantId,
            fixture.ProgramId, packageId));

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
    public async Task ShouldRejectSystemActorGivenPackageValidation()
    {
        // Arrange
        await using var fixture = new Fixture();
        var command = new ValidateAuditPackage(fixture.TenantId, fixture.ProgramId,
            Uuid.CreateVersion4(), 1, "validate-system-actor");
        var handler = new ValidateAuditPackageHandler(fixture.Executor, fixture.Reader,
            new AuditPackageSourceBuilder(fixture.Reader), TimeProvider.System);

        // Act
        var result = await handler.HandleAsync(Fixture.SystemContext(command),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
    }
}
