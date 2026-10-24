using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Snapshots;
using Bdgrz.Compliance.Tests.Features.AccessReviews;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

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
        var provider = ProgramManagementServices.Build(new AlwaysAllowed(), _ => { }, services =>
        {
            services.AddSingleton<AccessReviewManifests>();
            services.AddScoped<AccessReviewReader>();
        });
        var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var content = services.GetRequiredService<IImmutableContentStore>();
        var events = services.GetRequiredService<IEventStore>();
        var realm = TenantId.ToString();
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
        await events.AppendAsync(new EventStreamAddress(realm, "population-snapshots", CampaignSnapshotId.ToString()),
            0, new AggregateScenario<PopulationSnapshot>(launchSnapshot).PendingEvents.ToArray());

        // The launch fact, its item intent and the materialized item, with the recorded times the test controls.
        var campaignStream = AccessReviewEffects.CampaignStream(TenantId, CampaignId);
        var itemStream = AccessReviewEffects.ItemStream(TenantId, CampaignId, ItemId);
        var itemStreamId = AccessReviewEffects.ItemStreamId(TenantId, CampaignId, ItemId);
        var launchedFact = await AccessReviewCampaignFacts.LaunchedAsync(content, TenantId, CampaignId, header.Name, deadline,
            CampaignSnapshotId, launchDigest.Value.Sha256, reviewers, items, CampaignOwner, launchedAt, ProgramId,
            RemediationOwnerMemberId, header.Instructions);
        var intentHeader = new DurableIntentHeader(CampaignId, campaignStream, campaignStream, campaignStream,
            CampaignOwner, "initial", null, new string('d', 64), launchedAt);
        var entry = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), AccessReviewContent.ItemEntrySchema, item);
        var creationIntent = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(),
            AccessReviewItemCreationIntent.Schema, new AccessReviewItemCreationIntent(intentHeader, CampaignId, 1,
                ItemId, SystemInstanceId, entry, ReviewerMemberId,
                AccessReviewEffects.ReviewerEpisode(CampaignId, ItemId),
                new NativeFactReference(new EventStreamAddress(realm, "population-snapshots",
                    CampaignSnapshotId.ToString()), Uuid.CreateVersion4(), 1)));
        DomainEvent stableLaunch = launchedFact;
        stableLaunch.AttachMetadata(new DomainEventMetadata(LaunchEventId, CampaignId, 1, LaunchAt));
        var opened = new AccessReviewItemOpened(TenantId, CampaignId, ItemId, itemStream, 1, 1, creationIntent, null,
            new OperationProvenance(Uuid.CreateVersion4(), CampaignId, null, nameof(MaterializeAccessReviewItem),
                CampaignOwner, itemStream, "initial", null, new string('d', 64), launchedAt, itemStream, itemStream),
            null, null);
        DomainEvent openedFact = opened;
        openedFact.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), itemStreamId, 1, LaunchAt));
        var itemEvents = new List<DomainEvent> { openedFact };
        if (includeDecisionAfterTypeIDate)
        {
            var effectiveDecisionAt = decisionAt ?? AfterTypeIDate;
            var decision = new AccessDecisionView(Uuid.CreateVersion4(), ItemId, AccessReviewVocabulary.Keep,
                "The access remains required.", ReviewerMemberId, Reviewer, effectiveDecisionAt, null,
                effectiveDecisionAt > deadline, null);
            var decisions = await content.WriteJsonAsync<IReadOnlyList<AccessDecisionView>>(realm, Uuid.CreateVersion4(),
                AccessReviewContent.DecisionsSchema, [decision]);
            DomainEvent recorded = new AccessDecisionsRecorded(TenantId, CampaignId, ItemId, 2, decisions,
                new OperationProvenance(decision.DecisionId, decision.DecisionId, null, nameof(RecordAccessDecision),
                    Reviewer, itemStream, "initial", 1, new string('e', 64), effectiveDecisionAt, itemStream, itemStream),
                null, false, creationIntent, null, null);
            recorded.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), itemStreamId, 2,
                decisionRecordedAt ?? AfterTypeIDate));
            itemEvents.Add(recorded);
        }

        await events.AppendAsync(itemStream, 0, itemEvents.ToArray());
        var campaignEvents = new List<DomainEvent> { stableLaunch };
        await events.AppendAsync(campaignStream, 0, campaignEvents.ToArray());

        Uuid? finalSnapshotId = null;
        DateTimeOffset? completedAtValue = null;
        if (completeBeforeTypeIDate)
        {
            var completedAt = new DateTimeOffset(2027, 3, 11, 12, 0, 0, TimeSpan.Zero);
            var attestation = "All campaign decisions were reviewed.";
            var completionSnapshotId = Uuid.CreateVersion4();
            var snapshot = await services.GetRequiredService<AccessReviewReader>()
                .ReadCampaignAsync(TenantId, CampaignId);
            var resultHeader = new AccessReviewCampaignResultHeader(CampaignId, header.Name,
                deadline, CampaignSnapshotId, launchDigest.Value.Sha256, attestation);
            var resultRows = AccessReviewCampaignContent.ResultRows(resultHeader, snapshot.ItemStates(completedAt));
            var resultDigest = PopulationContentIdentity.Compute(AccessReviewCampaignContent.ResultKind, resultRows);
            Assert.True(resultDigest.IsSuccess, resultDigest.Error?.Message);
            var completionSnapshot = new PopulationSnapshot(TenantId, completionSnapshotId);
            Assert.True(completionSnapshot.Freeze(completionSnapshotId, null,
                AccessReviewCampaignContent.ResultKind, resultRows, resultDigest.Value.Sha256,
                null, CampaignOwner, completedAt).IsSuccess);
            await events.AppendAsync(new EventStreamAddress(realm, "population-snapshots",
                completionSnapshotId.ToString()), 0,
                new AggregateScenario<PopulationSnapshot>(completionSnapshot).PendingEvents.ToArray());

            var operationId = Uuid.CreateVersion4();
            var closureIntent = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), ClosureIntent.Schema,
                new UnitResult());
            var firstResponse = await FirstResponses.FreezeAsync(content, realm,
                await AccessReviewContent.AcceptedResponseAsync(content, TenantId, operationId, completedAt,
                    CancellationToken.None));
            DomainEvent accepted = new AccessReviewCampaignCompletionAccepted(campaignStream, 2, 1, closureIntent, null,
                new OperationProvenance(operationId, operationId, null, nameof(CompleteAccessReviewCampaign),
                    CampaignOwner, campaignStream, "initial", 1, new string('f', 64), completedAt, campaignStream,
                    campaignStream), firstResponse, null);
            accepted.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), CampaignId, 2, completedAt));
            var completion = new AccessReviewCampaignCompletionView(completionSnapshotId, resultDigest.Value.Sha256,
                attestation, CampaignOwner, completedAt);
            var stableCompletion = await AccessReviewCampaignFacts.CompletedAsync(content, TenantId, CampaignId, 3, completion,
                CampaignOwner);
            DomainEvent closed = stableCompletion with
            {
                Intent = closureIntent,
                Operation = stableCompletion.Operation with { RootOperationId = operationId },
            };
            closed.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), CampaignId, 3, completedAt));
            await events.AppendAsync(campaignStream, 1, [accepted, closed]);
            finalSnapshotId = completionSnapshotId;
            completedAtValue = completedAt;
        }

        var checkpoint = directoryCaughtUp
            ? await CheckpointAfterAsync((IDomainEventReader)events)
            : new ProjectionCheckpoint(EventCursor.Start);
        var directory = new CampaignDirectory(new AccessReviewCampaignSummaryView(TenantId, CampaignId, header.Name,
            deadline, finalSnapshotId is null ? AccessReviewCampaign.Active : AccessReviewCampaign.Completed, 1,
            CampaignSnapshotId, launchedAt, finalSnapshotId, completedAtValue), checkpoint);
        return new FixtureState(new AccessReviewCampaignsTypeIBaselineSourceContributor(directory,
            services.GetRequiredService<IAggregateReader>(), (IDomainEventReader)events,
            services.GetRequiredService<AccessReviewReader>(), content), scope, provider);
    }

    static async Task<ProjectionCheckpoint> CheckpointAfterAsync(IDomainEventReader events)
    {
        var cursor = EventCursor.Start;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(
                           TenantId.ToString(), AccessReviewCampaign.Area), cursor,
                           CancellationToken.None))
            cursor = record.NextCursor;
        return new ProjectionCheckpoint(cursor);
    }

    sealed record FixtureState(ITypeIBaselineSourceContributor Contributor, AsyncServiceScope Scope,
        ServiceProvider Provider);

    sealed class AlwaysAllowed : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(true);
    }

    sealed class CampaignDirectory(AccessReviewCampaignSummaryView summary,
        ProjectionCheckpoint checkpoint) : IAccessReviewCampaignDirectoryReader
    {
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(checkpoint);

        public ValueTask<Page<AccessReviewCampaignSummaryView>> ListAsync(Uuid tenantId,
            int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<AccessReviewCampaignSummaryView>([summary], null));
    }
}
