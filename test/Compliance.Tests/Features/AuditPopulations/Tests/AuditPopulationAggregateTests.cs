using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AuditPopulations.Aggregates;
using Bdgrz.Compliance.Features.AuditPopulations.Events;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
namespace Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;

public sealed class AuditPopulationAggregateTests
{
    [Fact]
    public void ShouldDeriveStreamFromTenantAndPopulationGivenDifferentSelectedCommands()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var populationId = Uuid.CreateVersion4();

        // Act
        var first = new AuditPopulation(tenantId, programId, populationId, Uuid.CreateVersion4());
        var second = new AuditPopulation(tenantId, programId, populationId, Uuid.CreateVersion4());

        // Assert
        Assert.Equal(first.Stream, second.Stream);
        Assert.Equal(populationId, first.Id);
        Assert.Equal(0, first.Revision);
        Assert.Null(first.Header);
    }

    [Fact]
    public void ShouldRefuseAndAppendNothingGivenLegacyFactWithoutCurrentContent()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var populationId = Uuid.CreateVersion4();
        var owner = new AuditPopulation(tenantId, Uuid.CreateVersion4(), populationId, Uuid.CreateVersion4());
        var legacy = new AuditPopulationFrozen(tenantId, populationId, 1, null, null!, DateTimeOffset.UnixEpoch);
        var cut = AuditPopulationPreparedCut.Of(owner);

        // Act
        var result = owner.Append(legacy, cut);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Empty(new AggregateScenario<AuditPopulation>(owner).PendingEvents);
        Assert.Equal(0, owner.Revision);
    }
}
