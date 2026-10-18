using Bdgrz.Compliance.Features.Examinations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Examinations;

public sealed class TypeIBaselinePreviewReaderTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldReportEveryUncoveredSourceFamilyGivenPreview()
    {
        // Arrange
        var engagement = Engagement();
        var reader = new TypeIBaselinePreviewReader([]);

        // Act
        var result = await reader.ReadAsync(engagement, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value.Sources);
        Assert.Equal(TypeIBaselineSourceFamilies.Required.Count + 1, result.Value.Blockers.Count);
        Assert.All(TypeIBaselineSourceFamilies.Required, family =>
            Assert.Contains(result.Value.Blockers, blocker => blocker.Contains(family,
                StringComparison.Ordinal)));
    }

    [Fact]
    public async Task ShouldBuildManifestOnlyAfterEverySourceFamilyIsCoveredGivenPreview()
    {
        // Arrange
        var engagement = Engagement();
        var boundary = new TypeIBaselineSourceReference("approved_boundary",
            engagement.BoundaryId, Uuid.CreateVersion4(), 2, new string('a', 64));
        var contributors = TypeIBaselineSourceFamilies.Required.Select(family =>
            (ITypeIBaselineSourceContributor)new StubContributor(family,
                family == "approved_boundary" ? [boundary] : []));
        var reader = new TypeIBaselinePreviewReader(contributors);

        // Act
        var result = await reader.ReadAsync(engagement, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value.Blockers);
        Assert.Equal([boundary], result.Value.Sources);
        Assert.Equal(TypeIBaselineSourceFamilies.Required.Count, result.Value.SourceCoverage.Count);
        Assert.Equal(64, result.Value.ContentSha256.Length);
    }

    [Fact]
    public void ShouldBindCompletedEmptyFamiliesIntoManifestGivenSourceCoverage()
    {
        // Arrange
        var engagement = Engagement();
        var boundary = new TypeIBaselineSourceReference("approved_boundary",
            engagement.BoundaryId, Uuid.CreateVersion4(), 2, new string('a', 64));
        var missingCoverage = TypeIBaselinePreview.Create(engagement.TenantId,
            engagement.ProgramId, engagement.ExaminationEngagementId, [boundary]);
        var coverage = TypeIBaselineSourceFamilies.Required.Select(family =>
            TypeIBaselineContentIdentity.Coverage(family,
                family == "approved_boundary" ? [boundary] : [])).ToArray();
        var completeEmptyFamilies = TypeIBaselinePreview.Create(engagement.TenantId,
            engagement.ProgramId, engagement.ExaminationEngagementId, [boundary], [], [], coverage);

        // Act
        var missingDigest = missingCoverage.ContentSha256;
        var coveredDigest = completeEmptyFamilies.ContentSha256;

        // Assert
        Assert.NotEqual(missingDigest, coveredDigest);
    }

    [Fact]
    public async Task ShouldResolveBoundaryVersionEffectiveOnTypeIDateGivenBoundaryContribution()
    {
        // Arrange
        var engagement = Engagement();
        var content = new Bdgrz.Compliance.Features.Boundaries.BoundaryContent(
            "In-scope system", "type_i", engagement.TrustServicesCategories, []);
        var boundaryVersion = new Bdgrz.Compliance.Features.Boundaries.BoundaryVersionView(
            engagement.TenantId, engagement.BoundaryId, engagement.ProgramId,
            Uuid.CreateVersion4(), 3, content, "approved", engagement.PeriodStart,
            Uuid.CreateVersion4(), "Boundary approver", Now);
        var boundary = new Bdgrz.Compliance.Features.Boundaries.BoundaryView(
            engagement.TenantId, engagement.BoundaryId, engagement.ProgramId, null,
            boundaryVersion, null, 4);
        var boundaryReader = new BoundaryReader(boundary, boundaryVersion);
        var consistency = new Bdgrz.Compliance.Features.Programs.ProgramSetupWorkReadConsistency(
            new ProgramReader(), boundaryReader,
            new Cntryl.Portia.Testing.InMemoryEventStore());
        var contributor = new BoundaryTypeIBaselineSourceContributor(boundaryReader, consistency);

        // Act
        var result = await contributor.ReadAsync(engagement, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var source = Assert.Single(result.Value.Sources);
        Assert.Equal("approved_boundary", source.Kind);
        Assert.Equal(engagement.BoundaryId, source.RecordId);
        Assert.Equal(boundaryVersion.VersionId, source.VersionId);
        Assert.Equal(boundaryVersion.Revision, source.Revision);
        Assert.Equal(Bdgrz.Compliance.Features.Snapshots.SnapshotContentIdentity
                .ApprovedBoundaryVersion(boundaryVersion),
            source.ContentSha256);
        Assert.Empty(result.Value.Blockers);
        Assert.Empty(result.Value.ScopeDifferences);
    }

    [Fact]
    public async Task ShouldBlockUnresolvedBoundaryEntryGivenBoundaryContribution()
    {
        // Arrange
        var engagement = Engagement();
        var entry = new Bdgrz.Compliance.Features.Boundaries.BoundaryScopeEntry(
            Uuid.CreateVersion4(), "service", "service", "Unresolved service", null,
            string.Empty, "Needs mapping", true);
        var content = new Bdgrz.Compliance.Features.Boundaries.BoundaryContent(
            "In-scope system", "type_i", engagement.TrustServicesCategories, [entry]);
        var boundaryVersion = new Bdgrz.Compliance.Features.Boundaries.BoundaryVersionView(
            engagement.TenantId, engagement.BoundaryId, engagement.ProgramId,
            Uuid.CreateVersion4(), 3, content, "approved", engagement.PeriodStart,
            Uuid.CreateVersion4(), "Boundary approver", Now);
        var boundary = new Bdgrz.Compliance.Features.Boundaries.BoundaryView(
            engagement.TenantId, engagement.BoundaryId, engagement.ProgramId, null,
            boundaryVersion, null, 4);
        var boundaryReader = new BoundaryReader(boundary, boundaryVersion);
        var consistency = new Bdgrz.Compliance.Features.Programs.ProgramSetupWorkReadConsistency(
            new ProgramReader(), boundaryReader,
            new Cntryl.Portia.Testing.InMemoryEventStore());
        var contributor = new BoundaryTypeIBaselineSourceContributor(boundaryReader, consistency);

        // Act
        var result = await contributor.ReadAsync(engagement, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Contains(result.Value.Blockers, blocker => blocker.Contains(entry.EntryId.ToString(),
            StringComparison.Ordinal));
        Assert.Contains(result.Value.ScopeDifferences, difference =>
            difference.Contains(entry.EntryId.ToString(), StringComparison.Ordinal));
    }

    static ExaminationEngagementView Engagement() => new(Uuid.CreateVersion4(),
        Uuid.CreateVersion4(), Uuid.CreateVersion4(), "type_i", "Audit Firm",
        new DateOnly(2026, 9, 30), null,
        ["security", "availability", "confidentiality", "processing_integrity", "privacy"],
        Uuid.CreateVersion4(), [], "draft", 1, Now, Uuid.CreateVersion4(), "Examiner");

    sealed class StubContributor(string family,
        IReadOnlyList<TypeIBaselineSourceReference> sources) : ITypeIBaselineSourceContributor
    {
        public string Family => family;

        public ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
            ExaminationEngagementView engagement, CancellationToken ct) =>
            ValueTask.FromResult(Result<TypeIBaselineSourceContribution>.Success(
                new TypeIBaselineSourceContribution(family, sources, [], [])));
    }

    sealed class ProgramReader : Bdgrz.Compliance.Features.Programs.IProgramDirectoryReader
    {
        public ValueTask<Bdgrz.Compliance.Features.Programs.ProgramView?> GetAsync(Uuid tenantId,
            Uuid programId, CancellationToken ct = default) => ValueTask.FromResult<
            Bdgrz.Compliance.Features.Programs.ProgramView?>(null);

        public ValueTask<Page<Bdgrz.Compliance.Features.Programs.ProgramView>> ListAsync(
            Uuid tenantId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<Bdgrz.Compliance.Features.Programs.ProgramView>([], null));

        public ValueTask<Page<Bdgrz.Compliance.Features.Programs.ProgramRevisionView>?>
            ListRevisionsAsync(Uuid tenantId, Uuid programId, int limit, string? cursor,
                CancellationToken ct = default) => ValueTask.FromResult<
                Page<Bdgrz.Compliance.Features.Programs.ProgramRevisionView>?>(null);

        public ValueTask<Bdgrz.Compliance.Features.Programs.ProgramRevisionView?> GetRevisionAsync(
            Uuid tenantId, Uuid programId, long revision, CancellationToken ct = default) =>
            ValueTask.FromResult<Bdgrz.Compliance.Features.Programs.ProgramRevisionView?>(null);

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);
    }

    sealed class BoundaryReader(Bdgrz.Compliance.Features.Boundaries.BoundaryView boundary,
        Bdgrz.Compliance.Features.Boundaries.BoundaryVersionView effectiveVersion)
        : Bdgrz.Compliance.Features.Boundaries.IBoundaryDirectoryReader
    {
        public ValueTask<Bdgrz.Compliance.Features.Boundaries.BoundaryView?> GetAsync(
            Uuid tenantId, Uuid boundaryId, CancellationToken ct = default) =>
            ValueTask.FromResult<Bdgrz.Compliance.Features.Boundaries.BoundaryView?>(
                tenantId == boundary.TenantId && boundaryId == boundary.BoundaryId ? boundary : null);

        public ValueTask<Page<Bdgrz.Compliance.Features.Boundaries.BoundaryView>> ListProgramAsync(
            Uuid tenantId, Uuid programId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(
            new Page<Bdgrz.Compliance.Features.Boundaries.BoundaryView>([], null));

        public ValueTask<Bdgrz.Compliance.Features.Boundaries.BoundaryVersionView?> GetVersionAsync(
            Uuid tenantId, Uuid boundaryId, Uuid versionId, CancellationToken ct = default) =>
            ValueTask.FromResult<Bdgrz.Compliance.Features.Boundaries.BoundaryVersionView?>(
                tenantId == effectiveVersion.TenantId && boundaryId == effectiveVersion.BoundaryId &&
                versionId == effectiveVersion.VersionId ? effectiveVersion : null);

        public ValueTask<Page<Bdgrz.Compliance.Features.Boundaries.BoundaryVersionView>?>
            ListVersionsAsync(Uuid tenantId, Uuid boundaryId, int limit, string? cursor,
                CancellationToken ct = default) => ValueTask.FromResult<
                Page<Bdgrz.Compliance.Features.Boundaries.BoundaryVersionView>?>(null);

        public ValueTask<Bdgrz.Compliance.Features.Boundaries.BoundaryVersionView?>
            GetEffectiveVersionAsync(Uuid tenantId, Uuid boundaryId, DateOnly effectiveOn,
                CancellationToken ct = default) => ValueTask.FromResult<
                Bdgrz.Compliance.Features.Boundaries.BoundaryVersionView?>(
                tenantId == effectiveVersion.TenantId && boundaryId == effectiveVersion.BoundaryId &&
                effectiveOn >= effectiveVersion.EffectiveFrom ? effectiveVersion : null);

        public ValueTask<Bdgrz.Compliance.Features.Boundaries.BoundaryDecisionView?> GetDecisionAsync(
            Uuid tenantId, Uuid boundaryId, Uuid decisionId, CancellationToken ct = default) =>
            ValueTask.FromResult<Bdgrz.Compliance.Features.Boundaries.BoundaryDecisionView?>(null);

        public ValueTask<Page<Bdgrz.Compliance.Features.Boundaries.BoundaryDecisionView>?>
            ListDecisionsAsync(Uuid tenantId, Uuid boundaryId, int limit, string? cursor,
                CancellationToken ct = default) => ValueTask.FromResult<
                Page<Bdgrz.Compliance.Features.Boundaries.BoundaryDecisionView>?>(null);

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);
    }
}
