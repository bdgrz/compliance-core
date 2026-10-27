using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Readers;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Events;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Providers.Tests;

public sealed class FitzProviderDirectoryTests
{
    static readonly ActorReference Author = NamedRecordFacts.Author;
    static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldExhaustScopedCurrentAndRevisionPagesGivenTwoTenantsAndRetainedProviderFacts()
    {
        // Arrange
        var directory = new FitzProviderDirectory(new InMemoryKvClient());
        Uuid[] tenants = [Uuid.CreateVersion4(), Uuid.CreateVersion4()];
        Uuid[][] ids = [[Uuid.CreateVersion4(), Uuid.CreateVersion4()], [Uuid.CreateVersion4(), Uuid.CreateVersion4()]];
        foreach (var index in Enumerable.Range(0, 2))
        {
            await using var batch = await directory.BeginAsync(new ProjectionBatchContext(Identity(tenants[index]), ProjectionCheckpoint.Start));
            for (var item = 0; item < 2; item++)
            {
                await directory.ApplyAsync(Recorded(tenants[index], ids[index][item]),
                    new ProviderContent($"Provider {item}", "Supplier"));
                await directory.ApplyAsync(Revised(tenants[index], ids[index][item], 2),
                    new ProviderContent($"Provider {item} revised", "Supplier"));
            }
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var all = new List<ProviderView>();
        for (var index = 0; index < 2; index++)
        {
            string? cursor = null;
            do
            {
                var page = await directory.ListAsync(tenants[index], 1, cursor);
                all.AddRange(page.Items);
                cursor = page.NextCursor;
            } while (cursor is not null);
            foreach (var id in ids[index])
            {
                var history = new List<ProviderView>();
                cursor = null;
                do
                {
                    var page = await directory.ListRevisionsAsync(tenants[index], id, 1, cursor);
                    history.AddRange(page.Items);
                    cursor = page.NextCursor;
                } while (cursor is not null);
                Assert.Equal([1L, 2L], history.Select(static view => view.Revision));
                Assert.All(history, view => { Assert.Equal(tenants[index], view.TenantId); Assert.Equal(id, view.ProviderId); });
                Assert.DoesNotContain("revised", history[0].Content.Name, StringComparison.Ordinal);
                var exact = await directory.GetRevisionAsync(tenants[index], id, 1);
                Assert.Equal(history[0].Content.Name, exact!.Content.Name);
                Assert.Equal(history[0].RecordedBy, exact.RecordedBy);
                Assert.Equal(history[0].RecordedAt, exact.RecordedAt);
            }
        }
        var first = await directory.ListAsync(tenants[0], 1, null);
        var revisions = await directory.ListRevisionsAsync(tenants[0], ids[0][0], 1, null);

        // Assert
        Assert.Equal(ids.SelectMany(static ids => ids).Order(), all.Select(static view => view.ProviderId).Order());
        Assert.All(all, view => { Assert.Equal(2, view.Revision); Assert.EndsWith("revised", view.Content.Name, StringComparison.Ordinal); Assert.Equal("manual", view.SourceKind); });
        Assert.Null(await directory.GetAsync(tenants[1], ids[0][0]));
        Assert.Null(await directory.GetRevisionAsync(tenants[1], ids[0][0], 1));
        await Assert.ThrowsAsync<KvDirectoryQueryException>(async () => await directory.ListAsync(tenants[1], 1, first.NextCursor));
        await Assert.ThrowsAsync<KvDirectoryQueryException>(async () => await directory.ListRevisionsAsync(tenants[0], ids[0][0], 1, first.NextCursor));
        await Assert.ThrowsAsync<KvDirectoryQueryException>(async () => await directory.ListRevisionsAsync(tenants[0], ids[0][1], 1, revisions.NextCursor));
    }

    [Fact]
    public async Task ShouldRetainHistoryAndRejectChangedReplayGivenCommittedProviderRevisions()
    {
        // Arrange
        var directory = new FitzProviderDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var first = Recorded(tenantId, id);
        var second = Revised(tenantId, id, 2);
        await using (var initial = await directory.BeginAsync(new ProjectionBatchContext(Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(first, new ProviderContent("Original", "Supplier"));
            await directory.ApplyAsync(second, new ProviderContent("Revised", "Supplier"));
            await initial.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        await using (var replay = await directory.BeginAsync(new ProjectionBatchContext(Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(first, new ProviderContent("Original", "Supplier"));
            await directory.ApplyAsync(second, new ProviderContent("Revised", "Supplier"));
            await replay.CommitAsync(ProjectionCheckpoint.Start);
        }
        await using (var changed = await directory.BeginAsync(new ProjectionBatchContext(Identity(tenantId), ProjectionCheckpoint.Start)))
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await directory.ApplyAsync(
                first, new ProviderContent("Replacement", "Supplier")));

        // Assert
        Assert.Equal("Original", (await directory.GetRevisionAsync(tenantId, id, 1))!.Content.Name);
        Assert.Equal("Revised", (await directory.GetAsync(tenantId, id))!.Content.Name);
        Assert.Equal(2, (await directory.ListRevisionsAsync(tenantId, id, 10, null)).Items.Count);
    }

    [Theory]
    [InlineData("foreign_tenant")]
    [InlineData("empty_tenant")]
    [InlineData("empty_provider")]
    public async Task ShouldRejectBeforeCommitGivenEventOutsideItsTenantBatch(string invalid)
    {
        // Arrange
        var directory = new FitzProviderDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var ev = Recorded(invalid == "foreign_tenant" ? Uuid.CreateVersion4() :
            invalid == "empty_tenant" ? Uuid.Empty : tenantId,
            invalid == "empty_provider" ? Uuid.Empty : Uuid.CreateVersion4());

        // Act
        await using var batch = await ((IProviderProjection)directory).BeginAsync(
            new ProjectionBatchContext(Identity(tenantId), ProjectionCheckpoint.Start));

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await directory.ApplyAsync(ev, new ProviderContent("Provider", "Supplier")));
    }

    [Fact]
    public async Task ShouldRecoverWithoutPartialRowsGivenAbortedProjectionBatch()
    {
        // Arrange
        var directory = new FitzProviderDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var first = Recorded(tenantId, id);
        var second = Revised(tenantId, id, 2);

        // Act
        await using (var interrupted = await directory.BeginAsync(new ProjectionBatchContext(Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(first, new ProviderContent("Provider", "Supplier"));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await directory.ApplyAsync(
                second with { Revision = 3 }, new ProviderContent("Revised", "Supplier")));
        }
        var before = await directory.GetAsync(tenantId, id);
        await using (var retry = await directory.BeginAsync(new ProjectionBatchContext(Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(first, new ProviderContent("Provider", "Supplier"));
            await directory.ApplyAsync(second, new ProviderContent("Revised", "Supplier"));
            await retry.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Assert
        Assert.Null(before);
        Assert.Equal(2, (await directory.GetAsync(tenantId, id))!.Revision);
        Assert.Equal(2, (await directory.ListRevisionsAsync(tenantId, id, 10, null)).Items.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ShouldRejectRevisionGivenNoRecordedPredecessor(long revision)
    {
        // Arrange
        var directory = new FitzProviderDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var ev = Revised(tenantId, Uuid.CreateVersion4(), revision);

        // Act
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(Identity(tenantId), ProjectionCheckpoint.Start));

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await directory.ApplyAsync(ev, new ProviderContent("Unrecorded", "Supplier")));
    }

    static ProviderRecorded Recorded(Uuid tenantId, Uuid providerId) =>
        NamedRecordFacts.ProviderRecordedFact(tenantId, providerId, Author, Now);

    static ProviderRevised Revised(Uuid tenantId, Uuid providerId, long revision) =>
        NamedRecordFacts.ProviderRevisedFact(tenantId, providerId, revision, Author, Now.AddMinutes(1));

    static CheckpointIdentity Identity(Uuid tenantId) => new(FitzProviderDirectory.ProjectorName,
        EventStreamPattern.ForPattern(tenantId.ToString(), Provider.Area));
}
