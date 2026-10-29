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
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Readers;
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
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
namespace Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;

public sealed class FitzInventoryRegisterDirectoryTests
{
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Recorder");
    static readonly Uuid Owner = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldExhaustScopedPagesAndHistoryGivenTwoTenantsOfLocationsAndProcesses()
    {
        // Arrange
        var directory = new FitzInventoryRegisterDirectory(new InMemoryKvClient());
        Uuid[] tenants = [Uuid.CreateVersion4(), Uuid.CreateVersion4()];
        Uuid[][] locations = [[Uuid.CreateVersion4(), Uuid.CreateVersion4()], [Uuid.CreateVersion4(), Uuid.CreateVersion4()]];
        Uuid[][] processes = [[Uuid.CreateVersion4(), Uuid.CreateVersion4()], [Uuid.CreateVersion4(), Uuid.CreateVersion4()]];
        foreach (var t in new[] { 0, 1 })
        {
            await using var batch = await directory.BeginAsync(new ProjectionBatchContext(Identity(tenants[t]), ProjectionCheckpoint.Start));
            for (var i = 0; i < 2; i++)
            {
                await directory.ApplyAsync(NamedRecordFacts.LocationFact(tenants[t], locations[t][i], 1, Author, Now),
                    Loc($"Site {i}"));
                await directory.ApplyAsync(NamedRecordFacts.LocationFact(tenants[t], locations[t][i], 2, Author,
                    Now.AddMinutes(1)), Loc($"Site {i} renamed"));
                await directory.ApplyAsync(NamedRecordFacts.ProcessFact(tenants[t], processes[t][i], 1, Author, Now),
                    Proc($"Process {i}"));
            }
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var seenLocations = new List<LocationView>();
        var seenProcesses = new List<OperationalProcessView>();
        string? cursor = null;
        do
        {
            var page = await directory.ListLocationsAsync(tenants[0], 1, cursor);
            seenLocations.AddRange(page.Items);
            cursor = page.NextCursor;
        } while (cursor is not null);
        do
        {
            var page = await directory.ListOperationalProcessesAsync(tenants[0], 1, cursor);
            seenProcesses.AddRange(page.Items);
            cursor = page.NextCursor;
        } while (cursor is not null);
        var history = await directory.ListLocationRevisionsAsync(tenants[0], locations[0][0], 10, null);
        var firstPage = await directory.ListLocationsAsync(tenants[0], 1, null);

        // Assert
        Assert.Equal(locations[0].Order(), seenLocations.Select(static view => view.LocationId).Order());
        Assert.All(seenLocations, view => { Assert.Equal(tenants[0], view.TenantId); Assert.Equal(2, view.Revision); });
        Assert.Equal(processes[0].Order(), seenProcesses.Select(static view => view.OperationalProcessId).Order());
        Assert.All(seenProcesses, view => Assert.Equal(tenants[0], view.TenantId));
        Assert.Equal([1L, 2L], history.Items.Select(static view => view.Revision));
        Assert.Equal("manual", history.Items[0].SourceKind);
        Assert.Null(await directory.GetLocationAsync(tenants[1], locations[0][0]));
        Assert.Null(await directory.GetOperationalProcessAsync(tenants[1], processes[0][0]));
        Assert.Empty((await directory.ListLocationRevisionsAsync(tenants[1], locations[0][0], 10, null)).Items);
        await Assert.ThrowsAsync<KvDirectoryQueryException>(async () =>
            await directory.ListLocationsAsync(tenants[1], 1, firstPage.NextCursor));
    }

    [Fact]
    public async Task ShouldRetainHistoryAndRejectChangedReplayGivenCommittedRevisions()
    {
        // Arrange
        var directory = new FitzInventoryRegisterDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var first = NamedRecordFacts.LocationFact(tenantId, id, 1, Author, Now);
        var second = NamedRecordFacts.LocationFact(tenantId, id, 2, Author, Now.AddMinutes(1));
        await using (var initial = await directory.BeginAsync(new ProjectionBatchContext(Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(first, Loc("Original"));
            await directory.ApplyAsync(second, Loc("Revised"));
            await initial.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        await using (var replay = await directory.BeginAsync(new ProjectionBatchContext(Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(first, Loc("Original"));
            await directory.ApplyAsync(second, Loc("Revised"));
            await replay.CommitAsync(ProjectionCheckpoint.Start);
        }
        await using (var changed = await directory.BeginAsync(new ProjectionBatchContext(Identity(tenantId), ProjectionCheckpoint.Start)))
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await directory.ApplyAsync(
                first, Loc("Replacement")));

        // Assert
        Assert.Equal("Revised", (await directory.GetLocationAsync(tenantId, id))!.Content.Name);
        Assert.Equal(2, (await directory.ListLocationRevisionsAsync(tenantId, id, 10, null)).Items.Count);
    }

    [Fact]
    public async Task ShouldRejectBeforeCommitGivenRevisionWithoutPredecessor()
    {
        // Arrange
        var directory = new FitzInventoryRegisterDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(Identity(tenantId), ProjectionCheckpoint.Start));

        // Act
        var gap = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await directory.ApplyAsync(NamedRecordFacts.ProcessFact(tenantId, Uuid.CreateVersion4(), 2, Author, Now),
                Proc("Orphan")));

        // Assert
        Assert.Contains("predecessor", gap.Message, StringComparison.Ordinal);
    }

    static LocationContent Loc(string name) => new("physical_site", name, null, Owner, "active");

    static OperationalProcessContent Proc(string name) =>
        new(name, "Purpose", Owner, null, null, "active");

    static CheckpointIdentity Identity(Uuid tenantId) => new(FitzInventoryRegisterDirectory.ProjectorName,
        EventStreamPattern.ForPattern(tenantId.ToString(), "inventory-registers"));
}
