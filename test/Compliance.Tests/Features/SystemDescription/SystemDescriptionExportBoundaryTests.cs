using Bdgrz.Compliance.Features.SystemDescription;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.SystemDescription;

public sealed class SystemDescriptionExportBoundaryTests
{
    [Fact]
    public async Task ShouldKeepBusinessRevisionGivenIndependentTechnicalExport()
    {
        // Arrange
        await using var fixture = new SystemDescriptionNativeFixture();
        await fixture.SeedAsync(approved: true);
        var before = (await fixture.Descriptions.ReadAsync(fixture.Tenant, fixture.Program, fixture.Description)).Revision;
        var request = new GenerateSystemDescriptionExport(Uuid.CreateVersion4(), fixture.Tenant, fixture.Program, fixture.Description, 1, "pdf", "exact-version-export");

        // Act
        var generated = await new GenerateSystemDescriptionExportHandler(fixture.Exports).HandleAsync(fixture.Context(request), CancellationToken.None);
        var after = await fixture.Descriptions.ReadAsync(fixture.Tenant, fixture.Program, fixture.Description);

        // Assert
        Assert.True(generated.IsSuccess, generated.Error?.Message);
        Assert.Equal(before, after.Revision);
        Assert.Equal(3, (await fixture.FactsAsync()).Count);
    }
}
