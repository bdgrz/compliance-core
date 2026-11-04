using Bdgrz.Compliance.Features.Workforce.Projections;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Workforce.Readers;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Tests.Features.Workforce.Tests;

public sealed class CassieWorkforceSourceDirectoryTests
{
    [Fact]
    public async Task ShouldRetainAcceptedSourceProvenanceAndIsolateTenantGivenProjectedDecision()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieWorkforceSchema.Statements);
        await using var directory = new CassieWorkforceSourceDirectory(database.Database);
        var tenantId = Uuid.CreateVersion4();
        var targetId = Uuid.CreateVersion4();
        var observationId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");
        var now = DateTimeOffset.UtcNow;
        var source = new WorkforceSourceIdentity("hris", "system", "100", "v1");
        var facts = new WorkforceSourceFacts(Person: new WorkforcePersonSourceFacts("Ada", null));
        var decision = new WorkforceSourceDecision("accepted", "Matches HRIS", 2, actor, now);
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         new CheckpointIdentity("WorkforceSourcesV1",
                             EventStreamPattern.ForPattern(tenantId.ToString(), "workforce-source-observations")),
                         ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new WorkforceSourceObserved(tenantId, observationId, source,
                "person", targetId, 1, facts, now, actor, now));
            await directory.ApplyAsync(new WorkforceSourceReconciled(tenantId, observationId, 2, decision));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var found = await directory.GetAsync(tenantId, observationId);
        var page = await directory.ListAsync(tenantId, "person", targetId, 50, null);
        var alien = await directory.GetAsync(Uuid.CreateVersion4(), observationId);

        // Assert
        Assert.Equal(source, found!.Source);
        Assert.Equal(facts, found.Facts);
        Assert.Equal(decision, found.Decision);
        Assert.Equal(2, found.Revision);
        Assert.Equal(found, Assert.Single(page.Items));
        Assert.Null(alien);
    }
}
