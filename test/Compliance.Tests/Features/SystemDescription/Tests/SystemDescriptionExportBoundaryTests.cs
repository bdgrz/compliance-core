using Cntryl.Portia;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Handlers;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;

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
