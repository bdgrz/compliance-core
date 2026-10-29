using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.TechnologyInventory.Readers;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
namespace Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;

public sealed class FitzTechnologyInventoryDirectoryTests
{
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");

    [Fact]
    public async Task ShouldProjectCurrentAndHistoryAndIsolateTenantsGivenAssetRevisions()
    {
        // Arrange
        var directory = new FitzTechnologyInventoryDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var assetId = Uuid.CreateVersion4();
        var first = new InformationAssetContent("Customer PII", "confidential", "M0-D16",
            Uuid.CreateVersion4(), null, "active");
        var second = first with { Classification = "restricted" };

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(InventoryFacts.AssetFact(tenantId, assetId, 1,
                first, Author, Now));
            await directory.ApplyAsync(InventoryFacts.AssetFact(tenantId, assetId, 2,
                second, Author, Now.AddMinutes(1)));
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await directory.ApplyAsync(InventoryFacts.AssetFact(tenantId,
                    assetId, 4, second, Author, Now)));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var current = await directory.GetAssetAsync(tenantId, assetId);
        var history = await directory.ListAssetRevisionsAsync(tenantId, assetId, 50, null);
        var listed = await directory.ListAssetsAsync(tenantId, 50, null);
        var alien = await directory.ListAssetsAsync(Uuid.CreateVersion4(), 50, null);

        // Assert
        Assert.NotNull(current);
        Assert.Equal(2, current.Revision);
        Assert.Equal("restricted", current.Content.Classification);
        Assert.Equal("manual", current.SourceKind);
        Assert.Equal([1L, 2L], history.Items.Select(static item => item.Revision));
        Assert.Equal("confidential", history.Items[0].Content.Classification);
        Assert.Single(listed.Items);
        Assert.Empty(alien.Items);
    }

    static CheckpointIdentity Identity(Uuid tenantId) =>
        new(FitzTechnologyInventoryDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "technology-inventory"));
}
