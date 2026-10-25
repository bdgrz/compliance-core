using Bdgrz.Compliance.Features.AuditPopulations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AuditPopulations;

public sealed partial class DefineAuditPopulationHandlerTests
{
    [Fact]
    public async Task ShouldRetainAllSeventyDefinitionsWithoutLifetimeCapGivenAuditPopulationNativeCapacity()
    {
        // Arrange
        await using var fixture = new Fixture();
        var close = await fixture.SeedClosedPeriodAsync();
        Assert.True((await fixture.Handler().HandleAsync(fixture.Context(new DefineAuditPopulation(fixture.TenantId,
            fixture.ProgramId, fixture.PopulationId, fixture.PeriodId, close, fixture.Definition())), CancellationToken.None)).IsSuccess);

        // Act
        for (var revision = 1; revision < 70; revision++)
        {
            fixture.AdvanceClock();
            var accepted = await ExecutePopulationAsync(fixture, new ReviseAuditPopulationDefinition(Uuid.CreateVersion4(), fixture.TenantId,
                fixture.ProgramId, fixture.PopulationId, revision, "Approved production changes", "Include approved changes.",
                "Exclude canceled changes.", $"Retained definition reason {revision}"), RequestMetadata.Create());
            Assert.True(accepted.IsSuccess, $"Definition {revision + 1}: {accepted.Error?.Message}");
        }
        var owner = await fixture.Reader.HydrateAsync(new Bdgrz.Compliance.Features.AuditPopulations.AuditPopulation(
            fixture.TenantId, fixture.ProgramId, fixture.PopulationId));

        // Assert
        Assert.Equal(70, owner.Revision);
        var complete = await fixture.Populations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PopulationId);
        Assert.Equal(70, complete.View!.DefinitionVersions.Count);
        Assert.Equal("Retained definition reason 1", complete.View.DefinitionVersions[1].ChangeReason);
        Assert.Equal(70, owner.Header!.DefinitionVersion);
    }
}
