using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Criteria.Readers;
using Bdgrz.Compliance.Features.Criteria.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
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
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaTextOverlayProjectionTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
    readonly IImmutableContentStore _content = new FitzKvImmutableContentStore(
        new PersistenceKvClient(new InMemoryKvClient()));

    [Fact]
    public async Task ShouldProjectLatestRevisionAndIsolateTenantsGivenOverlayEvents()
    {
        // Arrange
        var directory = new FitzCriteriaTextOverlayDirectory(new InMemoryKvClient(), _content);
        var tenantId = Uuid.CreateVersion4();
        var otherTenantId = Uuid.CreateVersion4();
        var editionId = Uuid.CreateVersion4();
        const string identifier = "CC6.1";
        var actorId = Uuid.CreateVersion4();
        var first = await RevisedAsync(tenantId, editionId, identifier, 1, "Original", actorId);
        var second = await RevisedAsync(tenantId, editionId, identifier, 2, "Updated", actorId,
            Now.AddMinutes(1));
        var otherTenant = await RevisedAsync(otherTenantId, editionId, identifier, 1, "Other tenant",
            Uuid.CreateVersion4());
        await ProjectAsync(directory, tenantId, first, second);
        await ProjectAsync(directory, otherTenantId, otherTenant);
        await ProjectAsync(directory, tenantId, first, second);

        // Act
        var current = await directory.GetManyAsync(tenantId, editionId, [identifier]);
        var other = await directory.GetManyAsync(otherTenantId, editionId, [identifier]);

        // Assert
        Assert.True(current.IsSuccess);
        Assert.True(other.IsSuccess);
        Assert.Equal(2, current.Value[identifier].Revision);
        Assert.Equal("Updated", current.Value[identifier].Content.Text);
        Assert.Equal(tenantId, current.Value[identifier].TenantId);
        Assert.Equal("Other tenant", other.Value[identifier].Content.Text);
        Assert.Equal(otherTenantId, other.Value[identifier].TenantId);
    }

    [Fact]
    public async Task ShouldRejectRevisionGapGivenOverlayEvent()
    {
        // Arrange
        var directory = new FitzCriteriaTextOverlayDirectory(new InMemoryKvClient(), _content);
        var tenantId = Uuid.CreateVersion4();
        var editionId = Uuid.CreateVersion4();
        var revised = await RevisedAsync(tenantId, editionId, "CC6.1", 2, "Skipped revision",
            Uuid.CreateVersion4());

        // Act
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
            Identity(tenantId), ProjectionCheckpoint.Start));

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await directory.ApplyAsync(revised));
    }

    [Fact]
    public async Task ShouldApplyCurrentUsePolicyGivenProjectedOverlay()
    {
        // Arrange
        var directory = new FitzCriteriaTextOverlayDirectory(new InMemoryKvClient(), _content);
        var tenantId = Uuid.CreateVersion4();
        var editionId = Uuid.CreateVersion4();
        var revised = await RevisedAsync(tenantId, editionId, "CC6.1", 1, "Licensed supplied wording",
            Uuid.CreateVersion4(), usageFlags: new CriteriaOverlayUsageFlags(Display: true,
                Export: false));
        await ProjectAsync(directory, tenantId, revised);
        var reader = new FitzCriteriaTextOverlayReader(directory,
            new CriteriaTextOverlayReadConsistency(directory, new EmptyEventReader()));
        var entry = new Criterion(editionId, "CC6.1", "CC6.1", "security", "criterion",
            null, "Summary", "Original licensed wording");

        // Act
        var display = await reader.ApplyAsync(tenantId, entry, isExport: false,
            CancellationToken.None);
        var export = await reader.ApplyAsync(tenantId, entry, isExport: true,
            CancellationToken.None);

        // Assert
        Assert.True(display.IsSuccess);
        Assert.Equal("Licensed supplied wording", display.Value.LicensedText);
        Assert.Equal(1, display.Value.Overlay?.Revision);
        Assert.True(export.IsSuccess);
        Assert.Null(export.Value.LicensedText);
        Assert.Equal("supplier", export.Value.Overlay?.Supplier);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenUnprojectedOverlayEvent()
    {
        // Arrange
        var directory = new FitzCriteriaTextOverlayDirectory(new InMemoryKvClient(), _content);
        var reader = new FitzCriteriaTextOverlayReader(directory,
            new CriteriaTextOverlayReadConsistency(directory, new PendingEventReader()));
        var entry = new Criterion(Uuid.CreateVersion4(), "CC6.1", "CC6.1", "security",
            "criterion", null, "Summary");

        // Act
        var result = await reader.ApplyAsync(Uuid.CreateVersion4(), entry, isExport: false,
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(result.Error?.IsTransient);
    }

    async Task<CriteriaTextOverlayEntryRevised> RevisedAsync(Uuid tenantId, Uuid editionId,
        string identifier, long revision, string text, Uuid actorId,
        DateTimeOffset? recordedAt = null, CriteriaOverlayUsageFlags? usageFlags = null)
    {
        var overlay = await NativeContent.WriteAsync(_content, tenantId, CriteriaTextOverlay.ContentSchema,
            new CriteriaTextOverlayContent(text, "supplier", "license-reference",
                usageFlags ?? new CriteriaOverlayUsageFlags(Display: true, Export: true)));
        var stream = new CriteriaTextOverlay(tenantId, editionId, identifier).Stream;
        return new CriteriaTextOverlayEntryRevised(tenantId, editionId, identifier, revision, overlay,
            RootOperation.Provenance(Uuid.CreateVersion4(), "SetCriteriaTextOverlay", stream,
                ActorReference.ForMember(actorId, "Overlay editor"), revision - 1, new string('c', 64),
                recordedAt ?? Now), NativeFacts.Ref(), NativeFacts.Ref());
    }

    static async Task ProjectAsync(FitzCriteriaTextOverlayDirectory directory, Uuid tenantId,
        params CriteriaTextOverlayEntryRevised[] events)
    {
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
            Identity(tenantId), ProjectionCheckpoint.Start));
        foreach (var domainEvent in events)
            await directory.ApplyAsync(domainEvent);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }

    static CheckpointIdentity Identity(Uuid tenantId) => new(
        FitzCriteriaTextOverlayDirectory.ProjectorName,
        EventStreamPattern.ForPattern(tenantId.ToString(), CriteriaTextOverlay.Area));

    sealed class EmptyEventReader : IDomainEventReader
    {
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream,
            ulong fromVersion, [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern,
            EventCursor after, [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    sealed class PendingEventReader : IDomainEventReader
    {
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream,
            ulong fromVersion, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern,
            EventCursor after, [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield return null!;
        }
    }
}
