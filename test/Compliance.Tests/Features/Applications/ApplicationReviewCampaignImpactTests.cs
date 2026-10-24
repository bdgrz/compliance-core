using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using System.Text.Json;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationReviewCampaignImpactTests
{
    [Fact]
    public async Task ShouldIncludeMinimalCampaignReferenceGivenAffectedSystemInstance()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var systemInstanceId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var campaignSnapshotId = Uuid.CreateVersion4();
        var launchedAt = DateTimeOffset.UtcNow.AddDays(-3);
        var source = CreateApplication(tenantId, applicationId);
        var sourceReader = new SourceReader(source);
        var kv = new InMemoryKvClient();
        var applications = new FitzApplicationDirectory(kv);
        var now = DateTimeOffset.UtcNow;
        await ProjectApplicationAsync(applications, tenantId, new ApplicationDeclared(tenantId,
            applicationId, "Payroll", "Run payroll", null, Uuid.CreateVersion4(), "Manager", now, ApplicationFacts.Application(tenantId, applicationId), null));
        await ProjectApplicationAsync(applications, tenantId, new SystemInstanceRegistered(
            tenantId, applicationId, systemInstanceId, 1, "Payroll production", "production",
            null, null, Uuid.CreateVersion4(), "Manager", now, ApplicationFacts.SystemInstance(tenantId, systemInstanceId), null));
        var boundaries = new FitzApplicationBoundaryReferenceDirectory(kv);
        var controls = new FitzApplicationControlDraftReferenceDirectory(kv);
        var events = new InMemoryEventStore();
        var populations = new StubPopulationDirectory([]);
        var campaigns = new StubCampaignImpactDirectory(
            [new AccessReviewCampaignSystemInstanceReferenceView(tenantId, campaignId,
                systemInstanceId, launchedAt)],
            [new AccessReviewCampaignSummaryView(tenantId, campaignId, "Quarterly access review",
                now.AddDays(7), AccessReviewCampaign.Active, 4, campaignSnapshotId, launchedAt,
                null, null)]);
        var handler = CreateHandler(tenantId, applicationId, sourceReader, applications,
            boundaries, controls, populations, campaigns, events);

        // Act
        var result = await handler.ReadForImportWorkerAsync(new PreviewApplicationChange(
            tenantId, applicationId, 1, "retire"), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var reference = Assert.Single(result.Value.ReviewCampaignReferences);
        Assert.Equal(campaignId, reference.CampaignId);
        Assert.Equal(systemInstanceId, reference.SystemInstanceId);
        Assert.Equal(campaignSnapshotId, reference.SnapshotId);
        Assert.Equal(AccessReviewCampaign.Active, reference.Status);
        Assert.Equal(launchedAt, reference.LaunchedAt);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result.Value,
            ComplianceCoreJsonContext.Default.ApplicationChangePreview));
        Assert.True(json.RootElement.GetProperty("review_campaign_references")[0]
            .TryGetProperty("final_snapshot_id", out _));
        Assert.DoesNotContain("review_campaigns", result.Value.PendingContexts);
        Assert.DoesNotContain("review_campaign_references_over_limit",
            result.Value.PendingContexts);
    }

    [Fact]
    public async Task ShouldKeepCampaignContextPendingGivenTruncatedCampaignPage()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var systemInstanceId = Uuid.CreateVersion4();
        var source = CreateApplication(tenantId, applicationId);
        var sourceReader = new SourceReader(source);
        var kv = new InMemoryKvClient();
        var applications = new FitzApplicationDirectory(kv);
        var now = DateTimeOffset.UtcNow;
        await ProjectApplicationAsync(applications, tenantId, new ApplicationDeclared(tenantId,
            applicationId, "Payroll", "Run payroll", null, Uuid.CreateVersion4(), "Manager", now, ApplicationFacts.Application(tenantId, applicationId), null));
        await ProjectApplicationAsync(applications, tenantId, new SystemInstanceRegistered(
            tenantId, applicationId, systemInstanceId, 1, "Payroll production", "production",
            null, null, Uuid.CreateVersion4(), "Manager", now, ApplicationFacts.SystemInstance(tenantId, systemInstanceId), null));
        var boundaries = new FitzApplicationBoundaryReferenceDirectory(kv);
        var controls = new FitzApplicationControlDraftReferenceDirectory(kv);
        var events = new InMemoryEventStore();
        var populations = new StubPopulationDirectory([]);
        var campaigns = new StubCampaignImpactDirectory([], nextCursor: "next");
        var handler = CreateHandler(tenantId, applicationId, sourceReader, applications,
            boundaries, controls, populations, campaigns, events);

        // Act
        var result = await handler.ReadForImportWorkerAsync(new PreviewApplicationChange(
            tenantId, applicationId, 1, "retire"), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Contains("review_campaigns", result.Value.PendingContexts);
        Assert.Contains("review_campaign_references_over_limit", result.Value.PendingContexts);
    }

    [Fact]
    public async Task ShouldReportCampaignProjectionLagGivenUnprojectedSourceWrite()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var campaigns = new StubCampaignImpactDirectory([], []);
        var events = new InMemoryEventStore();
        var consistency = new ApplicationReviewCampaignReadConsistency(campaigns, events);
        var launched = await Bdgrz.Compliance.Tests.Features.AccessReviews.AccessReviewCampaignFacts.LaunchedAsync(
            Bdgrz.Compliance.Tests.Features.AccessReviews.AccessReviewCampaignFacts.NewStore(), tenantId, campaignId,
            "Review", DateTimeOffset.UtcNow.AddDays(7), Uuid.CreateVersion4(), new string('a', 64), [], [],
            Bdgrz.Compliance.Features.AccessControl.ActorReference.ForMember(
                Uuid.CreateVersion4(), "Campaign owner"), DateTimeOffset.UtcNow);
        DomainEvent domainEvent = launched;
        domainEvent.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), campaignId,
            1, launched.LaunchedAt));
        await events.AppendAsync(new EventStreamAddress(tenantId.ToString(),
            AccessReviewCampaign.Area, campaignId.ToString()), 0, [domainEvent]);

        // Act
        var result = await consistency.CaptureAsync(tenantId, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
        Assert.True(result.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldReportCampaignProjectionRaceGivenSourceWriteAfterCapture()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var campaigns = new StubCampaignImpactDirectory([], []);
        var events = new InMemoryEventStore();
        var consistency = new ApplicationReviewCampaignReadConsistency(campaigns, events);
        var fence = await consistency.CaptureAsync(tenantId, CancellationToken.None);
        Assert.True(fence.IsSuccess);
        var launchedAt = DateTimeOffset.UtcNow;
        var launched = await Bdgrz.Compliance.Tests.Features.AccessReviews.AccessReviewCampaignFacts.LaunchedAsync(
            Bdgrz.Compliance.Tests.Features.AccessReviews.AccessReviewCampaignFacts.NewStore(), tenantId, campaignId,
            "Review", launchedAt.AddDays(7), Uuid.CreateVersion4(), new string('b', 64), [], [],
            Bdgrz.Compliance.Features.AccessControl.ActorReference.ForMember(
                Uuid.CreateVersion4(), "Campaign owner"), launchedAt);
        DomainEvent domainEvent = launched;
        domainEvent.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), campaignId,
            1, launchedAt));
        await events.AppendAsync(new EventStreamAddress(tenantId.ToString(),
            AccessReviewCampaign.Area, campaignId.ToString()), 0, [domainEvent]);

        // Act
        var result = await consistency.ConfirmUnchangedAndCaughtUpAsync(tenantId,
            fence.Value, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
        Assert.True(result.Error.IsTransient);
    }

    static PreviewApplicationChangeHandler CreateHandler(Uuid tenantId, Uuid applicationId,
        IAggregateReader sourceReader, FitzApplicationDirectory applications,
        FitzApplicationBoundaryReferenceDirectory boundaries,
        IApplicationControlDraftReferenceDirectory controls,
        IAccessPopulationDirectoryReader populations,
        IAccessReviewCampaignImpactDirectoryReader campaigns, InMemoryEventStore events)
    {
        var consistency = new SystemInstanceReadConsistency(applications, sourceReader,
            new LegacySystemInstanceSource(applications, events), events);
        return new PreviewApplicationChangeHandler(sourceReader, applications, boundaries,
            new ApplicationBoundaryReferenceReadConsistency(boundaries, events), controls,
            new ApplicationControlDraftReferenceReadConsistency(controls, events),
            RestrictedApplicationVisibilityFixture.Create(sourceReader),
            instanceConsistency: consistency,
            accessPopulations: populations,
            accessPopulationConsistency: new ApplicationAccessPopulationReadConsistency(
                populations, events),
            reviewCampaigns: campaigns,
            reviewCampaignConsistency: new ApplicationReviewCampaignReadConsistency(campaigns,
                events));
    }

    static DeclaredApplication CreateApplication(Uuid tenantId, Uuid applicationId)
    {
        var source = new DeclaredApplication(tenantId, applicationId);
        Assert.True(source.Declare("Payroll", "Run payroll", null, Uuid.CreateVersion4(),
            "Manager", DateTimeOffset.UtcNow).IsSuccess);
        return source;
    }

    static async Task ProjectApplicationAsync(FitzApplicationDirectory directory, Uuid tenantId,
        DomainEvent domainEvent)
    {
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity("ApplicationDirectoryV2",
                EventStreamPattern.ForPattern(tenantId.ToString())), ProjectionCheckpoint.Start));
        await directory.ApplyAsync(domainEvent);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }

    sealed class StubPopulationDirectory(IReadOnlyList<AccessPopulationSummaryView> items)
        : IAccessPopulationDirectoryReader
    {
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);

        public ValueTask<Page<AccessPopulationSummaryView>> ListAsync(Uuid tenantId,
            Uuid systemInstanceId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<AccessPopulationSummaryView>(items.Where(item =>
                item.TenantId == tenantId && item.SystemInstanceId == systemInstanceId)
                .Take(limit).ToArray(), null));
    }

    sealed class StubCampaignImpactDirectory(
        IReadOnlyList<AccessReviewCampaignSystemInstanceReferenceView> references,
        IReadOnlyList<AccessReviewCampaignSummaryView>? campaigns = null,
        string? nextCursor = null) : IAccessReviewCampaignImpactDirectoryReader
    {
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);

        public ValueTask<Page<AccessReviewCampaignSystemInstanceReferenceView>> ListBySystemInstanceAsync(
            Uuid tenantId, Uuid systemInstanceId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(
            new Page<AccessReviewCampaignSystemInstanceReferenceView>(references.Where(reference =>
                reference.TenantId == tenantId && reference.SystemInstanceId == systemInstanceId)
                .Take(limit).ToArray(), nextCursor));

        public ValueTask<AccessReviewCampaignSummaryView?> GetCampaignAsync(Uuid tenantId,
            Uuid campaignId, CancellationToken ct = default) => ValueTask.FromResult(
            campaigns?.FirstOrDefault(campaign => campaign.TenantId == tenantId &&
                campaign.CampaignId == campaignId));
    }

    sealed class SourceReader(Aggregate source) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult((TAggregate)source);
    }
}
