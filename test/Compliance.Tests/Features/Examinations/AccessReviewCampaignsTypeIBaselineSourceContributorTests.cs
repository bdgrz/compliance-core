using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Examinations;

public sealed class AccessReviewCampaignsTypeIBaselineSourceContributorTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid CampaignId = Uuid.CreateVersion4();
    static readonly Uuid PopulationId = Uuid.CreateVersion4();
    static readonly Uuid PopulationSnapshotId = Uuid.CreateVersion4();
    static readonly Uuid CampaignSnapshotId = Uuid.CreateVersion4();
    static readonly Uuid SystemInstanceId = Uuid.CreateVersion4();
    static readonly Uuid SubjectPersonId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerMemberId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerUserId = Uuid.CreateVersion4();
    static readonly Uuid RemediationOwnerMemberId = Uuid.CreateVersion4();
    static readonly Uuid CampaignOwnerMemberId = Uuid.CreateVersion4();
    static readonly Uuid ItemId = Uuid.CreateVersion5(CampaignId,
        $"{PopulationId}\nalice\nadmin");
    static readonly Uuid LaunchEventId = Uuid.CreateVersion4();
    static readonly DateOnly TypeIDate = new(2027, 3, 31);
    static readonly DateTimeOffset LaunchAt = new(2027, 2, 15, 12, 0, 0, TimeSpan.Zero);
    static readonly DateTimeOffset AfterTypeIDate = new(2027, 4, 1, 12, 0, 0,
        TimeSpan.Zero);
    static readonly DateTimeOffset RecordedBeforeTypeIDate = new(2027, 3, 31, 23, 0, 0,
        TimeSpan.Zero);
    static readonly ActorReference CampaignOwner = ActorReference.ForMember(CampaignOwnerMemberId,
        "Campaign owner");
    static readonly ActorReference Reviewer = ActorReference.ForMember(ReviewerMemberId, "Reviewer");

    [Fact]
    public async Task ShouldKeepCampaignBaselineStableGivenBackdatedDecisionRecordedAfterTypeIDate()
    {
        // Arrange
        var before = await Fixture(LaunchAt, includeDecisionAfterTypeIDate: false);
        var after = await Fixture(LaunchAt, includeDecisionAfterTypeIDate: true,
            decisionAt: new DateTimeOffset(2027, 3, 20, 12, 0, 0, TimeSpan.Zero),
            decisionRecordedAt: AfterTypeIDate);

        // Act
        var original = await before.Contributor.ReadAsync(Engagement(), CancellationToken.None);
        var later = await after.Contributor.ReadAsync(Engagement(), CancellationToken.None);

        // Assert
        Assert.True(original.IsSuccess, original.Error?.Message);
        Assert.True(later.IsSuccess, later.Error?.Message);
        Assert.Equal("access_review_campaigns", original.Value.Family);
        var source = Assert.Single(original.Value.Sources);
        var laterSource = Assert.Single(later.Value.Sources);
        Assert.Equal("access_review_campaign", source.Kind);
        Assert.Equal(CampaignId, source.RecordId);
        Assert.Equal(1, source.Revision);
        Assert.Equal(source.VersionId, laterSource.VersionId);
        Assert.Equal(source.ContentSha256, laterSource.ContentSha256);
        Assert.Empty(original.Value.Blockers);
        Assert.Empty(later.Value.Blockers);
    }

    [Fact]
    public async Task ShouldKeepCampaignBaselineStableGivenDecisionTimeAfterTypeIDate()
    {
        // Arrange
        var before = await Fixture(LaunchAt, includeDecisionAfterTypeIDate: false);
        var after = await Fixture(LaunchAt, includeDecisionAfterTypeIDate: true,
            decisionAt: AfterTypeIDate, decisionRecordedAt: RecordedBeforeTypeIDate);

        // Act
        var original = await before.Contributor.ReadAsync(Engagement(), CancellationToken.None);
        var later = await after.Contributor.ReadAsync(Engagement(), CancellationToken.None);

        // Assert
        Assert.True(original.IsSuccess, original.Error?.Message);
        Assert.True(later.IsSuccess, later.Error?.Message);
        Assert.Equal(1, Assert.Single(original.Value.Sources).Revision);
        var source = Assert.Single(original.Value.Sources);
        var laterSource = Assert.Single(later.Value.Sources);
        Assert.Equal(source.VersionId, laterSource.VersionId);
        Assert.Equal(source.ContentSha256, laterSource.ContentSha256);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenCampaignDirectoryBehindEvents()
    {
        // Arrange
        var fixture = await Fixture(LaunchAt, includeDecisionAfterTypeIDate: false,
            directoryCaughtUp: false);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
        Assert.True(result.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldExcludeCampaignGivenLaunchAfterTypeIDate()
    {
        // Arrange
        var fixture = await Fixture(AfterTypeIDate, includeDecisionAfterTypeIDate: false);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value.Sources);
        Assert.Empty(result.Value.Blockers);
    }

    [Fact]
    public async Task ShouldIncludeCompletedCampaignGivenIntactFinalSnapshot()
    {
        // Arrange
        var fixture = await Fixture(LaunchAt, includeDecisionAfterTypeIDate: true,
            decisionAt: new DateTimeOffset(2027, 3, 10, 12, 0, 0, TimeSpan.Zero),
            decisionRecordedAt: new DateTimeOffset(2027, 3, 10, 12, 0, 0, TimeSpan.Zero),
            completeBeforeTypeIDate: true);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var source = Assert.Single(result.Value.Sources);
        Assert.Equal(3, source.Revision);
        Assert.Empty(result.Value.Blockers);
        Assert.Empty(result.Value.ScopeDifferences);
    }

    static ExaminationEngagementView Engagement() => new(TenantId, ProgramId,
        Uuid.CreateVersion4(), "type_i", "Audit Firm", TypeIDate, null, ["security"],
        Uuid.CreateVersion4(), [], "draft", 1,
        new DateTimeOffset(TypeIDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc)),
        CampaignOwnerMemberId, "Examiner");

    static async Task<FixtureState> Fixture(DateTimeOffset launchedAt,
        bool includeDecisionAfterTypeIDate, DateTimeOffset? decisionAt = null,
        DateTimeOffset? decisionRecordedAt = null, bool directoryCaughtUp = true,
        bool completeBeforeTypeIDate = false)
    {
        var deadline = launchedAt.AddDays(60);
        var reviewer = new AccessReviewerView(PopulationId, SystemInstanceId,
            ReviewerMemberId, false, null);
        var item = new AccessReviewItemView(ItemId, PopulationId, PopulationSnapshotId,
            SystemInstanceId, ReviewerMemberId, "alice", "user_account", "Alice",
            AccessReviewVocabulary.Human, SubjectPersonId, null, "admin",
            "admin_privilege", "Administrator", true,
            AccessVarianceEvaluator.Unexpected,
            [new EffectiveAccessPathView([new EffectiveAccessHop("access_assignment",
                "alice", "admin")], null)], null);
        var reviewers = new[] { reviewer };
        var items = new[] { item };
        var header = new AccessReviewCampaignHeader(CampaignId, "Q1 access review",
            "Review every assigned access item.", deadline, ProgramId,
            RemediationOwnerMemberId);
        var launchRows = AccessReviewCampaignContent.LaunchRows(header, reviewers, items);
        var launchDigest = PopulationContentIdentity.Compute(AccessReviewCampaignContent.LaunchKind,
            launchRows);
        Assert.True(launchDigest.IsSuccess, launchDigest.Error?.Message);
        var launchSnapshot = new PopulationSnapshot(TenantId, CampaignSnapshotId);
        Assert.True(launchSnapshot.Freeze(CampaignSnapshotId, null,
            AccessReviewCampaignContent.LaunchKind, launchRows, launchDigest.Value.Sha256, null,
            CampaignOwner, launchedAt).IsSuccess);

        var campaign = new AccessReviewCampaign(TenantId, CampaignId);
        Assert.True(campaign.Launch(header.Name, header.Instructions, deadline, CampaignSnapshotId,
            launchDigest.Value.Sha256, reviewers, items, CampaignOwner, launchedAt, ProgramId,
            RemediationOwnerMemberId).IsSuccess);
        if (includeDecisionAfterTypeIDate)
            Assert.True(campaign.Decide([ItemId], campaign.Revision, Uuid.CreateVersion4(),
                AccessReviewVocabulary.Keep, "The access remains required.", ReviewerMemberId,
                ReviewerUserId, Reviewer, decisionAt ?? AfterTypeIDate, null, null).IsSuccess);

        PopulationSnapshot? completionSnapshot = null;
        if (completeBeforeTypeIDate)
        {
            var completedAt = new DateTimeOffset(2027, 3, 11, 12, 0, 0, TimeSpan.Zero);
            var attestation = "All campaign decisions were reviewed.";
            var completionSnapshotId = Uuid.CreateVersion4();
            var resultHeader = new AccessReviewCampaignResultHeader(CampaignId, header.Name,
                deadline, CampaignSnapshotId, launchDigest.Value.Sha256, attestation);
            var resultRows = AccessReviewCampaignContent.ResultRows(resultHeader,
                campaign.ItemStates(completedAt));
            var resultDigest = PopulationContentIdentity.Compute(
                AccessReviewCampaignContent.ResultKind, resultRows);
            Assert.True(resultDigest.IsSuccess, resultDigest.Error?.Message);
            completionSnapshot = new PopulationSnapshot(TenantId, completionSnapshotId);
            Assert.True(completionSnapshot.Freeze(completionSnapshotId, null,
                AccessReviewCampaignContent.ResultKind, resultRows, resultDigest.Value.Sha256,
                null, CampaignOwner, completedAt).IsSuccess);
            Assert.True(campaign.Complete(campaign.Revision, completionSnapshotId,
                resultDigest.Value.Sha256, attestation, CampaignOwner, completedAt).IsSuccess);
        }

        var events = new InMemoryEventStore();
        var snapshotEvents = new AggregateScenario<PopulationSnapshot>(launchSnapshot).PendingEvents;
        await events.AppendAsync(new EventStreamAddress(TenantId.ToString(), "population-snapshots",
            CampaignSnapshotId.ToString()), 0, snapshotEvents.ToArray());
        if (completionSnapshot is not null)
        {
            var completionSnapshotEvents = new AggregateScenario<PopulationSnapshot>(
                completionSnapshot).PendingEvents;
            await events.AppendAsync(new EventStreamAddress(TenantId.ToString(),
                "population-snapshots", completionSnapshot.Id.ToString()), 0,
                completionSnapshotEvents.ToArray());
        }
        var pendingCampaignEvents = new AggregateScenario<AccessReviewCampaign>(campaign)
            .PendingEvents.ToArray();
        var recordedLaunch = (AccessReviewCampaignLaunched)pendingCampaignEvents[0];
        DomainEvent stableLaunch = new AccessReviewCampaignLaunched(recordedLaunch.TenantId,
            recordedLaunch.CampaignId, recordedLaunch.Name, recordedLaunch.Instructions,
            recordedLaunch.Deadline, recordedLaunch.SnapshotId, recordedLaunch.ContentSha256,
            recordedLaunch.Reviewers, recordedLaunch.Items, recordedLaunch.LaunchedBy,
            recordedLaunch.LaunchedAt, recordedLaunch.ProgramId,
            recordedLaunch.RemediationOwnerMemberId);
        stableLaunch.AttachMetadata(new DomainEventMetadata(LaunchEventId, CampaignId, 1,
            LaunchAt));
        DomainEvent[] campaignEvents;
        if (pendingCampaignEvents.Length == 1)
        {
            campaignEvents = [stableLaunch];
        }
        else if (pendingCampaignEvents.Length >= 2 &&
                 pendingCampaignEvents[1] is AccessDecisionsRecorded pendingDecision)
        {
            var effectiveDecisionAt = decisionAt ?? AfterTypeIDate;
            var backdatedDecision = new AccessDecisionsRecorded(pendingDecision.TenantId,
                pendingDecision.CampaignId, pendingDecision.Revision,
                pendingDecision.Decisions.Select(decision => decision with
                {
                    DecidedAt = effectiveDecisionAt,
                    AfterDeadline = effectiveDecisionAt > deadline,
                }).ToArray());
            backdatedDecision.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(),
                CampaignId, 2, decisionRecordedAt ?? AfterTypeIDate));
            if (pendingCampaignEvents.Length == 2)
            {
                campaignEvents = [stableLaunch, backdatedDecision];
            }
            else if (pendingCampaignEvents is [_, _, AccessReviewCampaignCompleted completed])
            {
                DomainEvent stableCompletion = new AccessReviewCampaignCompleted(
                    completed.TenantId, completed.CampaignId, completed.Revision,
                    completed.Completion);
                stableCompletion.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(),
                    CampaignId, 3, completed.Completion.CompletedAt));
                campaignEvents = [stableLaunch, backdatedDecision, stableCompletion];
            }
            else
            {
                throw new InvalidOperationException("The campaign fixture produced unexpected events.");
            }
        }
        else
        {
            throw new InvalidOperationException("The campaign fixture produced unexpected events.");
        }
        await events.AppendAsync(new EventStreamAddress(TenantId.ToString(),
            AccessReviewCampaign.Area, CampaignId.ToString()), 0, campaignEvents);
        var replayed = new AggregateScenario<AccessReviewCampaign>(
            new AccessReviewCampaign(TenantId, CampaignId)).Given(campaignEvents).Aggregate;
        var checkpoint = directoryCaughtUp
            ? await CheckpointAfterAsync(events)
            : new ProjectionCheckpoint(EventCursor.Start);
        var directory = new CampaignDirectory(Summary(replayed), checkpoint);
        var aggregates = new List<Aggregate> { replayed, launchSnapshot };
        if (completionSnapshot is not null)
            aggregates.Add(completionSnapshot);
        var reader = new AggregateReader(aggregates.ToArray());
        return new FixtureState(new AccessReviewCampaignsTypeIBaselineSourceContributor(directory,
            reader, events));
    }

    static AccessReviewCampaignSummaryView Summary(AccessReviewCampaign campaign)
    {
        var launched = campaign.Launched!;
        var completion = campaign.Completion;
        return new AccessReviewCampaignSummaryView(TenantId, campaign.Id, launched.Name,
            launched.Deadline, campaign.Status, launched.Items.Count, launched.SnapshotId,
            launched.LaunchedAt, completion?.SnapshotId, completion?.CompletedAt);
    }

    static async Task<ProjectionCheckpoint> CheckpointAfterAsync(InMemoryEventStore events)
    {
        var cursor = EventCursor.Start;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(
                           TenantId.ToString(), AccessReviewCampaign.Area), cursor,
                           CancellationToken.None))
            cursor = record.NextCursor;
        return new ProjectionCheckpoint(cursor);
    }

    sealed record FixtureState(ITypeIBaselineSourceContributor Contributor);

    sealed class CampaignDirectory(AccessReviewCampaignSummaryView summary,
        ProjectionCheckpoint checkpoint) : IAccessReviewCampaignDirectoryReader
    {
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(checkpoint);

        public ValueTask<Page<AccessReviewCampaignSummaryView>> ListAsync(Uuid tenantId,
            int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<AccessReviewCampaignSummaryView>([summary], null));
    }

    sealed class AggregateReader(params Aggregate[] aggregates) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult(aggregates.FirstOrDefault(item => item.Id == aggregate.Id &&
                item.GetType() == typeof(TAggregate)) is TAggregate found ? found : aggregate);
    }
}
