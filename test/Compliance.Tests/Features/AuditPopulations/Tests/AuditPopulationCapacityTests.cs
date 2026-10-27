using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
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
using Bdgrz.Compliance.Features.AuditPopulations.Aggregates;
using Bdgrz.Compliance.Features.AuditPopulations.Commands;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;

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
        var owner = await fixture.Reader.HydrateAsync(new Bdgrz.Compliance.Features.AuditPopulations.Aggregates.AuditPopulation(
            fixture.TenantId, fixture.ProgramId, fixture.PopulationId));

        // Assert
        Assert.Equal(70, owner.Revision);
        var complete = await fixture.Populations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PopulationId);
        Assert.Equal(70, complete.View!.DefinitionVersions.Count);
        Assert.Equal("Retained definition reason 1", complete.View.DefinitionVersions[1].ChangeReason);
        Assert.Equal(70, owner.Header!.DefinitionVersion);
    }
}
