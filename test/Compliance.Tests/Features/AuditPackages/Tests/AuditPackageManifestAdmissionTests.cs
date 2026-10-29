using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;

public sealed class AuditPackageManifestAdmissionTests
{
    [Fact]
    public void ShouldDeriveSameStreamGivenDifferentSelectedCommandsAndVersions()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var package = Uuid.CreateVersion4();
        var first = new AuditPackageManifest(tenant, package);
        var second = new AuditPackageManifest(tenant, package, Uuid.CreateVersion4(), "AmendAuditPackage", 2);

        // Act
        var firstStream = first.Stream;
        var secondStream = second.Stream;

        // Assert
        Assert.Equal(firstStream, secondStream);
        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public void ShouldRefuseCreateGivenMissingProgramIdentity()
    {
        // Arrange
        var owner = new AuditPackageManifest(Uuid.CreateVersion4(), Uuid.CreateVersion4());

        // Act
        var result = owner.PrepareCreate(Uuid.Empty);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.False(owner.IsCreated);
    }

    [Fact]
    public async Task ShouldRefuseCreateGivenExistingPackageIdentity()
    {
        // Arrange
        await using var fixture = new AuditPackageCommandHandlerTests.Fixture();
        var package = await fixture.SeedBuiltAndValidatedPackageAsync();
        var owner = await fixture.Reader.HydrateAsync(new AuditPackageManifest(fixture.TenantId, package));

        // Act
        var result = owner.PrepareCreate(fixture.ProgramId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
    }

    [Fact]
    public async Task ShouldRefuseWriteGivenStaleRevisionWrongProgramOrNonCurrentVersion()
    {
        // Arrange
        await using var fixture = new AuditPackageCommandHandlerTests.Fixture();
        var package = await fixture.SeedBuiltAndValidatedPackageAsync();
        var owner = await fixture.Reader.HydrateAsync(new AuditPackageManifest(fixture.TenantId, package));

        // Act
        var stale = owner.PrepareWrite(fixture.ProgramId, owner.Revision - 1, owner.CurrentVersion);
        var foreign = owner.PrepareWrite(Uuid.CreateVersion4(), owner.Revision, owner.CurrentVersion);
        var future = owner.PrepareWrite(fixture.ProgramId, owner.Revision, owner.CurrentVersion + 1);
        var exact = owner.PrepareWrite(fixture.ProgramId, owner.Revision, owner.CurrentVersion);

        // Assert
        Assert.False(stale.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error!.Kind);
        Assert.False(foreign.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, foreign.Error!.Kind);
        Assert.False(future.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, future.Error!.Kind);
        Assert.True(exact.IsSuccess);
    }
}
