using System.Security.Claims;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Readers;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Boundaries.Services;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Services;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
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
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.Boundaries.Handlers;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Boundaries.Queries;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Responsibilities.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Boundaries.Tests;

public sealed class BoundaryConsumerAuthorityTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldClassifySourceFailureGivenCompactBoundaryRead(bool unavailable)
    {
        // Arrange
        await using var native = new BoundaryProjectionScenario();
        var tenant = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var rules = new BoundaryRules(tenant, id);
        Assert.True(rules.Create(Uuid.CreateVersion4(), Uuid.CreateVersion5(id, "draft-1"),
            new BoundaryContent("Verified source", "readiness", ["security"], []), Uuid.CreateVersion4(), "Author", DateTimeOffset.UtcNow).IsSuccess);
        var facts = await native.PublishAsync(rules);
        var fault = new FaultingContent(native.Content, unavailable);
        var sources = new BoundaryReader(native.Reader, native.Events, fault);
        var directory = new FitzBoundaryDirectory(native.Kv, sources);
        await native.ProjectAsync(directory, directory.ApplyAsync, FitzBoundaryDirectory.ProjectorName, tenant, facts);
        var handler = new GetBoundaryHandler(directory, sources);

        // Act
        var result = await handler.HandleAsync(new RequestContext<GetBoundary>(new GetBoundary(tenant, id), new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(unavailable, result.Error?.IsTransient);
        Assert.True(fault.ReadAttempted);
    }

    [Fact]
    public async Task ShouldUseNativeContentGivenForgedImpactDiscoveryBody()
    {
        // Arrange
        await using var native = new BoundaryProjectionScenario();
        var tenant = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var rules = new BoundaryRules(tenant, id);
        Assert.True(rules.Create(Uuid.CreateVersion4(), Uuid.CreateVersion5(id, "draft-1"),
            new BoundaryContent("Verified source", "readiness", ["security"], []), Uuid.CreateVersion4(), "Author", DateTimeOffset.UtcNow).IsSuccess);
        await native.PublishAsync(rules);
        var exact = (await native.Boundaries.ReadAsync(tenant, id)).ToView()!;
        var discovered = exact with { Draft = exact.Draft! with { Content = exact.Draft.Content with { Statement = "Forged discovery body" } } };
        var service = new BoundaryImpactService(new Discovery(discovered), native.Boundaries, [new ProgramBoundaryImpactContributor()]);

        // Act
        var result = await service.PreviewAsync(new PreviewBoundaryImpact(tenant, id, exact.Draft!.VersionId, 1), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("Verified source", Assert.Single(result.Value.Changes, change => change.Field == "statement").ProposedValue);
    }

    [Fact]
    public async Task ShouldExcludeNativeAuthorGivenForgedWorkDiscoveryAuthor()
    {
        // Arrange
        await using var native = new BoundaryProjectionScenario();
        var tenant = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var version = Uuid.CreateVersion5(id, "draft-1");
        var author = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var rules = new BoundaryRules(tenant, id);
        Assert.True(rules.Create(Uuid.CreateVersion4(), version,
            new BoundaryContent("Verified source", "readiness", ["security"], []), author, "Author", now).IsSuccess);
        Assert.Null(rules.AssignResponsibility(new ResponsibilityScope("boundary", id, version, 1), Uuid.CreateVersion4(), author,
            ResponsibilityType.AssignedReviewer, Uuid.CreateVersion4(), "Administrator", now, now, null, []));
        await native.PublishAsync(rules);
        var exact = (await native.Boundaries.ReadAsync(tenant, id)).ToView()!;
        var discovered = exact with { Draft = exact.Draft! with { AuthorMemberId = Uuid.CreateVersion4() } };

        // Act
        var result = await BoundaryDecisionWork.LoadAsync(native.Boundaries, new Discovery(discovered), tenant, rules.ProgramId, now, null, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value);
    }

    sealed class FaultingContent(IImmutableContentStore inner, bool unavailable) : IImmutableContentStore
    {
        public bool ReadAttempted { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) => inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default)
        {
            ReadAttempted = true;
            return Task.FromException<ReadOnlyMemory<byte>>(unavailable
                ? new ImmutableContentUnavailableException("The exact native source body is not visible yet.")
                : new ImmutableContentIntegrityException("The exact native source body is corrupt."));
        }
    }

    sealed class Discovery(BoundaryView view) : IBoundaryDirectoryReader
    {
        public ValueTask<BoundaryView?> GetAsync(Uuid tenantId, Uuid boundaryId, CancellationToken ct = default) => ValueTask.FromResult<BoundaryView?>(view);
        public ValueTask<Page<BoundaryView>> ListProgramAsync(Uuid tenantId, Uuid programId, int limit, string? cursor, CancellationToken ct = default) => ValueTask.FromResult(new Page<BoundaryView>([view], null));
        public ValueTask<BoundaryVersionView?> GetVersionAsync(Uuid tenantId, Uuid boundaryId, Uuid versionId, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask<Page<BoundaryVersionView>?> ListVersionsAsync(Uuid tenantId, Uuid boundaryId, int limit, string? cursor, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask<BoundaryVersionView?> GetEffectiveVersionAsync(Uuid tenantId, Uuid boundaryId, DateOnly effectiveOn, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask<BoundaryDecisionView?> GetDecisionAsync(Uuid tenantId, Uuid boundaryId, Uuid decisionId, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask<Page<BoundaryDecisionView>?> ListDecisionsAsync(Uuid tenantId, Uuid boundaryId, int limit, string? cursor, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);
    }
}
