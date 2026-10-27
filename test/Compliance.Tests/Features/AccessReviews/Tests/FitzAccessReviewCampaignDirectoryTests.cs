using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessReviews.Readers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
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
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
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
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AccessReviews.Projections;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;

public sealed class FitzAccessReviewCampaignDirectoryTests
{
    static readonly int[] LaunchOffsets = [0, 2, 1];
    [Fact]
    public async Task ShouldPageNewestCompletedCampaignsGivenPrefixCompatibleBrokerTraversal()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var content = AccessReviewCampaignFacts.NewStore();
        var directory = new FitzAccessReviewCampaignDirectory(new PrefixCompatibleKvClient(new InMemoryKvClient()),
            content);
        var now = new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero);
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Reviewer");
        var launched = new List<AccessReviewCampaignLaunched>();
        foreach (var offset in LaunchOffsets)
            launched.Add(await AccessReviewCampaignFacts.LaunchedAsync(content, tenantId, Uuid.CreateVersion4(),
                $"Campaign {offset}", now.AddDays(30), Uuid.CreateVersion4(), new string('a', 64), [], [], actor,
                now.AddMinutes(offset)));
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity(AccessReviewDirectorySchema.CampaignProjector,
                AccessReviewDirectorySchema.CampaignPattern(tenantId)), ProjectionCheckpoint.Start)))
        {
            foreach (var campaign in launched)
                await directory.ApplyAsync(campaign);
            await directory.ApplyAsync(await AccessReviewCampaignFacts.CompletedAsync(content, tenantId,
                launched[1].CampaignId, 2, new AccessReviewCampaignCompletionView(Uuid.CreateVersion4(),
                    new string('b', 64), "Complete", actor, now.AddDays(1)), actor));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var first = await directory.ListAsync(tenantId, 2, null);
        var second = await directory.ListAsync(tenantId, 2, first.NextCursor);

        // Assert
        Assert.Equal(new[] { launched[1].CampaignId, launched[2].CampaignId },
            first.Items.Select(static row => row.CampaignId));
        Assert.Equal(AccessReviewCampaign.Completed, first.Items[0].Status);
        Assert.NotNull(first.Items[0].FinalSnapshotId);
        Assert.Equal(launched[0].CampaignId, Assert.Single(second.Items).CampaignId);
        Assert.Null(second.NextCursor);
        Assert.Empty((await directory.ListAsync(Uuid.CreateVersion4(), 2, null)).Items);
    }

    sealed class PrefixCompatibleKvClient(IKvClient inner) : IKvClient
    {

        public async Task<IKvTransaction> BeginAsync(string route,
            KvMode mode = KvMode.ReadWrite, CancellationToken ct = default) =>
            new PrefixCompatibleTransaction(await inner.BeginAsync(route, mode, ct));

        public Task<KvSubscription> SubscribeAsync(string pattern, CancellationToken ct = default) =>
            inner.SubscribeAsync(pattern, ct);

        sealed class PrefixCompatibleTransaction(IKvTransaction inner) : IKvTransaction
        {
            public string Route => inner.Route;
            public Task<KvGetResult> GetAsync(ReadOnlyMemory<byte> key, CancellationToken ct = default) =>
                inner.GetAsync(key, ct);
            public Task PutAsync(ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value,
                CancellationToken ct = default) => inner.PutAsync(key, value, ct);
            public Task InsertAsync(ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value,
                CancellationToken ct = default) => inner.InsertAsync(key, value, ct);
            public Task DeleteAsync(ReadOnlyMemory<byte> key, CancellationToken ct = default) =>
                inner.DeleteAsync(key, ct);
            public Task DeleteRangeAsync(ReadOnlyMemory<byte> startKey, ReadOnlyMemory<byte> endKey,
                CancellationToken ct = default) => inner.DeleteRangeAsync(startKey, endKey, ct);
            public async Task<KvScanResult> ScanAsync(KvScanQuery query, CancellationToken ct = default)
            {
                // Reproduce the deployed broker's empty reverse prefix-range traversal (#498).
                if (query.Reverse && query.StartKey is not null && query.EndKey is not null)
                    return new KvScanResult([], false);
                var result = await inner.ScanAsync(query, ct);

                return result;
            }
            public Task CommitAsync(KvDurability durability, CancellationToken ct = default) =>
                inner.CommitAsync(durability, ct);
            public Task RollbackAsync(CancellationToken ct = default) => inner.RollbackAsync(ct);
            public ValueTask DisposeAsync() => inner.DisposeAsync();
        }
    }
}
