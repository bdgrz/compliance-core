using System.Security.Claims;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.History;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.Boundaries.Handlers;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Boundaries.Queries;
using Bdgrz.Compliance.Features.Boundaries.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Boundaries.Tests;

public sealed class BoundaryProjectionFreshnessTests
{
    [Theory]
    [InlineData("version")]
    [InlineData("versions")]
    [InlineData("effective_version")]
    public async Task ShouldReportProjectionLagGivenApprovedBoundaryHistoryRead(string read)
    {
        // Arrange
        await using var native = new BoundaryProjectionScenario();
        var tenantId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var versionId = Uuid.CreateVersion5(boundaryId, "draft-1");
        var authorId = Uuid.CreateVersion4();
        var reviewerId = Uuid.CreateVersion4();
        var reviewId = Uuid.CreateVersion4();
        var content = new BoundaryContent("System boundary", "readiness", ["security"], []);
        var source = new BoundaryRules(tenantId, boundaryId);
        Assert.True(source.Create(programId, versionId, content, authorId, "Author",
            DateTimeOffset.UtcNow).IsSuccess);
        Assert.Null(source.Review(versionId, 1, reviewId, "accept", "Reviewed",
            reviewerId, "Reviewer", DateTimeOffset.UtcNow));
        Assert.Null(source.Approve(versionId, 1, Uuid.CreateVersion4(), reviewId,
            new DateOnly(2027, 1, 1), "Approved", "digest", reviewerId, "Reviewer",
            DateTimeOffset.UtcNow));
        var directory = new EmptyBoundaryDirectory
        {
            Current = new BoundaryView(tenantId, boundaryId, programId,
                new BoundaryVersionView(tenantId, boundaryId, programId, versionId, 1,
                    content, "draft", null, authorId, "Author", DateTimeOffset.UtcNow),
                null, null, 2),
        };
        await native.PublishAsync(source);
        var consistency = new BoundaryHistoryReadConsistency(directory, native.Boundaries);

        // Act
        var lagged = await ReadErrorAsync(read, tenantId, boundaryId, versionId, 3,
            directory, consistency);
        directory.Current = directory.Current with { Draft = null, Revision = 3 };
        directory.Version = new BoundaryVersionView(tenantId, boundaryId, programId,
            versionId, 1, content, "approved", new DateOnly(2027, 1, 1), authorId,
            "Author", DateTimeOffset.UtcNow);
        var caughtUp = await ReadErrorAsync(read, tenantId, boundaryId, versionId, 3,
            directory, consistency);
        var future = await ReadErrorAsync(read, tenantId, boundaryId, versionId, 4,
            directory, consistency);
        var invalid = await ReadErrorAsync(read, tenantId, boundaryId, versionId, 0,
            directory, consistency);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, lagged?.Kind);
        Assert.NotNull(lagged);
        Assert.Contains("projection", lagged.Message, StringComparison.Ordinal);
        Assert.Null(caughtUp);
        Assert.Equal(RequestErrorKind.Conflict, future?.Kind);
        Assert.NotNull(future);
        Assert.Contains("source", future.Message, StringComparison.Ordinal);
        Assert.Equal(RequestErrorKind.Validation, invalid?.Kind);
    }

    static async ValueTask<RequestError?> ReadErrorAsync(string read, Uuid tenantId,
        Uuid boundaryId, Uuid versionId, long minimumRevision,
        EmptyBoundaryDirectory directory, BoundaryHistoryReadConsistency consistency) =>
        read switch
        {
            "version" => (await new GetBoundaryVersionHandler(directory, consistency).HandleAsync(
                new RequestContext<GetBoundaryVersion>(new GetBoundaryVersion(tenantId,
                    boundaryId, versionId, minimumRevision), new ClaimsPrincipal()),
                CancellationToken.None)).Error,
            "versions" => (await new ListBoundaryVersionsHandler(directory, consistency).HandleAsync(
                new RequestContext<ListBoundaryVersions>(new ListBoundaryVersions(tenantId,
                    boundaryId, MinimumBoundaryRevision: minimumRevision), new ClaimsPrincipal()),
                CancellationToken.None)).Error,
            _ => (await new GetEffectiveBoundaryVersionHandler(directory, consistency).HandleAsync(
                new RequestContext<GetEffectiveBoundaryVersion>(new GetEffectiveBoundaryVersion(
                    tenantId, boundaryId, new DateOnly(2027, 1, 1), minimumRevision),
                    new ClaimsPrincipal()), CancellationToken.None)).Error,
        };

    [Fact]
    public async Task ShouldReportProjectionLagGivenBoundaryEventBeforeProjection()
    {
        // Arrange
        await using var native = new BoundaryProjectionScenario();
        var tenantId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var boundary = new BoundaryRules(tenantId, boundaryId);
        Assert.True(boundary.Create(Uuid.CreateVersion4(), Uuid.CreateVersion5(boundaryId, "draft-1"),
            new BoundaryContent("System boundary", "readiness", ["security"], []),
            Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow).IsSuccess);
        await native.PublishAsync(boundary);
        var handler = new GetBoundaryHandler(new EmptyBoundaryDirectory(), native.Boundaries);

        // Act
        var result = await handler.HandleAsync(new RequestContext<GetBoundary>(
            new GetBoundary(tenantId, boundaryId, 1), new ClaimsPrincipal()),
            CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.Contains("projection", error.Message, StringComparison.Ordinal);
    }

    sealed class EmptyBoundaryDirectory : IBoundaryDirectoryReader
    {
        public BoundaryView? Current { get; set; }
        public BoundaryVersionView? Version { get; set; }

        public ValueTask<BoundaryView?> GetAsync(Uuid tenantId, Uuid boundaryId,
            CancellationToken ct = default) => ValueTask.FromResult(Current);

        public ValueTask<Page<BoundaryView>> ListProgramAsync(Uuid tenantId,
            Uuid programId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<BoundaryView>([], null));

        public ValueTask<BoundaryVersionView?> GetVersionAsync(Uuid tenantId,
            Uuid boundaryId, Uuid versionId, CancellationToken ct = default) =>
            ValueTask.FromResult(Version);

        public ValueTask<Page<BoundaryVersionView>?> ListVersionsAsync(Uuid tenantId,
            Uuid boundaryId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult<Page<BoundaryVersionView>?>(Current is null ? null :
                new Page<BoundaryVersionView>(Version is null ? [] : [Version], null));

        public ValueTask<BoundaryVersionView?> GetEffectiveVersionAsync(Uuid tenantId,
            Uuid boundaryId, DateOnly effectiveOn, CancellationToken ct = default) =>
            ValueTask.FromResult(Version);

        public ValueTask<BoundaryDecisionView?> GetDecisionAsync(Uuid tenantId,
            Uuid boundaryId, Uuid decisionId, CancellationToken ct = default) =>
            ValueTask.FromResult<BoundaryDecisionView?>(null);

        public ValueTask<Page<BoundaryDecisionView>?> ListDecisionsAsync(Uuid tenantId,
            Uuid boundaryId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult<Page<BoundaryDecisionView>?>(null);

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);
    }
}
