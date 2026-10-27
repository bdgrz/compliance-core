using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Queries;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Snapshots.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;

public sealed class AccessPopulationHandlerTests
{
    [Fact]
    public async Task ShouldFreezeTraceableImmutableSnapshotGivenAttestedPopulation()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();

        // Act
        var (populationId, accepted) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        var view = await fixture.SendAsync(fixture.ManagerUserId,
            new GetAccessPopulation(fixture.TenantId, populationId));
        var snapshot = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new PopulationSnapshot(fixture.TenantId, accepted.SnapshotId));

        // Assert
        Assert.Equal("accepted", view.Status);
        Assert.Equal(accepted.SnapshotId, view.SnapshotId);
        Assert.Equal(accepted.ContentSha256, view.ContentSha256);
        Assert.Equal(AccessPopulationAnalysis.CalculationId(accepted.ContentSha256), view.CalculationId);
        Assert.Equal("manual_attestation", view.SourceKind);
        Assert.Equal(1, view.SystemInstanceRevision);
        Assert.Equal(7, view.Facts.Principals.Count);
        Assert.Contains(view.EffectiveAccess, row => row.ProviderSubjectId == "ada" &&
                                                    row.ProviderEntitlementId == "admin");
        Assert.True(snapshot.HasIntactContent);
        Assert.Equal(AccessPopulationContent.Kind, snapshot.Kind);
        Assert.Contains(snapshot.Rows, row => row.Key == "principal/ada");
        await fixture.FailAsync(fixture.ManagerUserId, new RecordAccessPopulationFacts(Uuid.CreateVersion4(),
            fixture.TenantId, populationId, view.Revision, [], [], [], []), RequestErrorKind.Conflict);
    }

    [Fact]
    public async Task ShouldShowIssuesAndRefuseAcceptanceGivenIncompleteFacts()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var opened = await fixture.SendAsync(fixture.ManagerUserId,
            fixture.Open(AccessReviewFixture.Observed, "Manual export"));
        await fixture.SendAsync(fixture.ManagerUserId, new RecordAccessPopulationFacts(Uuid.CreateVersion4(),
            fixture.TenantId, opened.PopulationId, 1, [new("ada", "user_account", "Ada", "active")],
            [], [], [new("ada", "missing")]));

        // Act
        var preview = await fixture.SendAsync(fixture.ManagerUserId,
            new PreviewAccessPopulation(fixture.TenantId, opened.PopulationId));
        var refused = await fixture.FailAsync(fixture.ManagerUserId, new AcceptAccessPopulation(
            Uuid.CreateVersion4(), fixture.TenantId, opened.PopulationId, 2, "Attested."),
            RequestErrorKind.Validation);

        // Assert
        Assert.False(preview.CanAccept);
        Assert.Contains(preview.Issues, issue => issue.Category == "incomplete");
        Assert.Contains("issue", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldRejectOpenGivenStaleRevisionUnknownInstanceOrFutureObservation()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();

        // Act
        var stale = await fixture.FailAsync(fixture.ManagerUserId,
            fixture.Open(AccessReviewFixture.Observed, "Export", systemInstanceRevision: 2),
            RequestErrorKind.Conflict);
        var unknown = await fixture.FailAsync(fixture.ManagerUserId,
            fixture.Open(AccessReviewFixture.Observed, "Export", systemInstanceId: Uuid.CreateVersion4()),
            RequestErrorKind.NotFound);
        var future = await fixture.FailAsync(fixture.ManagerUserId,
            fixture.Open(DateTimeOffset.UtcNow.AddDays(1), "Export"), RequestErrorKind.Validation);

        // Assert
        Assert.Contains("revision", stale.Message, StringComparison.Ordinal);
        Assert.NotNull(unknown);
        Assert.NotNull(future);
    }

    [Fact]
    public async Task ShouldDenyOrHideGivenMissingPermissionOrAnotherTenant()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());

        // Act
        var denied = await fixture.FailAsync(fixture.OutsiderUserId,
            new GetAccessPopulation(fixture.TenantId, populationId), RequestErrorKind.Forbidden);
        var hidden = await fixture.FailAsync(fixture.ManagerUserId,
            new GetAccessPopulation(Uuid.CreateVersion4(), populationId), RequestErrorKind.NotFound);

        // Assert
        Assert.NotNull(denied);
        Assert.NotNull(hidden);
    }

    [Fact]
    public async Task ShouldReplayAcceptanceGivenRetriedRequest()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var opened = await fixture.SendAsync(fixture.ManagerUserId,
            fixture.Open(AccessReviewFixture.Observed, "Export"));
        var facts = AccessReviewFixture.StandardFacts();
        await fixture.SendAsync(fixture.ManagerUserId, fixture.Record(opened.PopulationId, 1, facts));
        var accept = new AcceptAccessPopulation(Uuid.CreateVersion4(), fixture.TenantId, opened.PopulationId, 2,
            "Attested.");

        // Act
        var first = await PersonalAccessReviewTransportTests.SendHttpAsync(fixture.Provider,
            fixture.ManagerUserId, accept);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var second = await PersonalAccessReviewTransportTests.SendHttpAsync(fixture.Provider,
            fixture.ManagerUserId, accept);
        Assert.True(second.IsSuccess, second.Error?.Message);

        // Assert
        Assert.Equal(first.Value.SnapshotId, second.Value.SnapshotId);
        Assert.Equal(first.Value.ContentSha256, second.Value.ContentSha256);
    }
}
