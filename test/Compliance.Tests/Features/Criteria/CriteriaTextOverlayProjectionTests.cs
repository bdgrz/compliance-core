using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Criteria;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Criteria;

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
