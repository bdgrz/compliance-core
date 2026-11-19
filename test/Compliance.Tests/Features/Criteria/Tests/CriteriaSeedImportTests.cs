using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaSeedImportTests
{
    [Fact]
    public void ShouldReplayCompletionGivenPublicationIsUnchangedAndRejectChangedPublication()
    {
        // Arrange
        var edition = PlatformCriteriaCatalog.SeedEdition();
        var fingerprint = CriteriaSeedFingerprint.Compute(edition, PlatformCriteriaCatalog.EnumerateSeedEntries());
        var operation = PlatformCriteriaSeedDefinition.OperationOf(edition.EditionId, "complete");
        var request = Uuid.CreateVersion4();
        var publication = Uuid.CreateVersion4();
        var first = new CriteriaSeedImport(edition.EditionId);
        var source = new AggregateScenario<CriteriaSeedImport>(first);
        Assert.True(first.Complete(operation, request, CriteriaSeedFingerprint.Version, fingerprint, publication).IsSuccess);
        var replay = new CriteriaSeedImport(edition.EditionId);
        var scenario = new AggregateScenario<CriteriaSeedImport>(replay).Given(source.PendingEvents.ToArray());

        // Act
        var retry = replay.Complete(operation, request, CriteriaSeedFingerprint.Version, fingerprint, publication);
        var changed = replay.Complete(operation, request, CriteriaSeedFingerprint.Version, fingerprint, Uuid.CreateVersion4());

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.False(changed.IsSuccess);
        Assert.IsType<CriteriaSeedImportCompleted>(Assert.Single(source.PendingEvents));
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRefuseCompletionGivenDifferentCompiledSeed()
    {
        // Arrange
        var edition = PlatformCriteriaCatalog.SeedEdition();
        var operation = PlatformCriteriaSeedDefinition.OperationOf(edition.EditionId, "complete");
        var owner = new CriteriaSeedImport(edition.EditionId);
        var scenario = new AggregateScenario<CriteriaSeedImport>(owner);

        // Act
        var result = owner.Complete(operation, Uuid.CreateVersion4(), CriteriaSeedFingerprint.Version,
            new string('a', 64), Uuid.CreateVersion4());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
    }
}
