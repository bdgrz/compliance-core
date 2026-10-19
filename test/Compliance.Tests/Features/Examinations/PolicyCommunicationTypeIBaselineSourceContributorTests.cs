using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Examinations;

public sealed class PolicyCommunicationTypeIBaselineSourceContributorTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid PolicyId = Policy.IdFor(TenantId, ProgramId, "POL-COMMS");
    static readonly Uuid RequirementId = TrainingCatalog.IdFor(TenantId, ProgramId, "TR-SEC");
    static readonly Uuid CampaignId = Uuid.CreateVersion4();
    static readonly Uuid RosterSnapshotId = Uuid.CreateVersion4();
    static readonly Uuid Ada = Uuid.CreateVersion4();
    static readonly Uuid Bob = Uuid.CreateVersion4();
    static readonly Uuid AdaAcknowledgementId = Uuid.CreateVersion4();
    static readonly Uuid BobAcknowledgementId = Uuid.CreateVersion4();
    static readonly Uuid AdaCompletionId = Uuid.CreateVersion4();
    static readonly Uuid BobCompletionId = Uuid.CreateVersion4();
    static readonly Uuid AuthorId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerId = Uuid.CreateVersion4();
    static readonly Uuid ApproverId = Uuid.CreateVersion4();
    static readonly Uuid ManagerId = Uuid.CreateVersion4();
    static readonly DateOnly TypeIDate = new(2027, 3, 31);
    static readonly DateTimeOffset LaunchAt = new(2027, 2, 15, 12, 0, 0, TimeSpan.Zero);
    static readonly DateTimeOffset AfterTypeIDate = new(2027, 4, 1, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Manager = ActorReference.ForMember(ManagerId, "Manager");

    [Fact]
    public async Task ShouldKeepPolicyCommunicationStableGivenAcknowledgementAfterTypeIDate()
    {
        // Arrange
        var before = await PolicyCampaign(includeLateResult: false);
        var after = await PolicyCampaign(includeLateResult: true);

        // Act
        var original = await before.Contributor.ReadAsync(Engagement(), before.Context,
            CancellationToken.None);
        var later = await after.Contributor.ReadAsync(Engagement(), after.Context,
            CancellationToken.None);
        var direct = await before.Contributor.ReadAsync(Engagement(), CancellationToken.None);

        // Assert
        Assert.True(original.IsSuccess, original.Error?.Message);
        Assert.True(later.IsSuccess, later.Error?.Message);
        Assert.True(direct.IsSuccess, direct.Error?.Message);
        Assert.Equal("policy_communications", original.Value.Family);
        var source = Assert.Single(original.Value.Sources);
        var laterSource = Assert.Single(later.Value.Sources);
        Assert.Equal("policy_communication", source.Kind);
        Assert.Equal(CampaignId, source.RecordId);
        Assert.Equal(source.VersionId, laterSource.VersionId);
        Assert.Equal(source.ContentSha256, laterSource.ContentSha256);
        Assert.Equal(source.ContentSha256, Assert.Single(direct.Value.Sources).ContentSha256);
        Assert.Empty(original.Value.Blockers);
        Assert.Empty(later.Value.Blockers);
    }

    [Fact]
    public async Task ShouldCaptureReconciledClosureTotalsGivenClosureBeforeTypeIDate()
    {
        // Arrange
        var fixture = await PolicyCampaign(includeLateResult: false, closeBeforeTypeIDate: true);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var source = Assert.Single(result.Value.Sources);
        Assert.Equal("policy_communication", source.Kind);
        Assert.Equal(4, source.Revision);
        Assert.Empty(result.Value.ScopeDifferences);
    }

    [Fact]
    public async Task ShouldRejectDifferentCampaignAudienceGivenExactPolicyVersion()
    {
        // Arrange
        var fixture = await PolicyCampaign(includeLateResult: false,
            audienceKind: PolicyAudience.RoleTargeted, audienceTeams: ["Finance"]);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
    }

    [Fact]
    public async Task ShouldCaptureTrainingVersionAsOfTypeIDateGivenLaterRequirementAndCompletion()
    {
        // Arrange
        var before = await TrainingCampaign(includeLaterChanges: false);
        var after = await TrainingCampaign(includeLaterChanges: true);

        // Act
        var original = await before.Contributor.ReadAsync(Engagement(), before.Context,
            CancellationToken.None);
        var later = await after.Contributor.ReadAsync(Engagement(), after.Context,
            CancellationToken.None);

        // Assert
        Assert.True(original.IsSuccess, original.Error?.Message);
        Assert.True(later.IsSuccess, later.Error?.Message);
        Assert.Equal("policy_communications", original.Value.Family);
        var requirement = Assert.Single(original.Value.Sources,
            item => item.Kind == "training_requirement_version");
        Assert.Equal(RequirementId, requirement.RecordId);
        Assert.Equal(Uuid.CreateVersion5(RequirementId,
            "training_requirement_version_v1:1"), requirement.VersionId);
        var campaign = Assert.Single(original.Value.Sources,
            item => item.Kind == "policy_communication");
        var laterCampaign = Assert.Single(later.Value.Sources,
            item => item.Kind == "policy_communication");
        Assert.Equal(campaign.ContentSha256, laterCampaign.ContentSha256);
        Assert.Equal(2, later.Value.Sources.Count);
        Assert.Empty(later.Value.Blockers);
    }

    static ExaminationEngagementView Engagement() => new(TenantId, ProgramId,
        Uuid.CreateVersion4(), "type_i", "Audit Firm", TypeIDate, null, ["security"],
        Uuid.CreateVersion4(), [], "draft", 1, LaunchAt, ManagerId, "Examiner");

    static TypeIBaselineSourceReadContext PolicyContext(Policy policy)
    {
        var references = PolicyTypeIBaselineSourceContributor.ReferencesAsOf(TenantId, policy,
            TypeIDate, new DateTimeOffset(TypeIDate.ToDateTime(TimeOnly.MaxValue,
                DateTimeKind.Utc)), out var valid);
        Assert.True(valid);
        return new TypeIBaselineSourceReadContext(Uuid.CreateVersion4(), references);
    }

    static async Task<FixtureState> PolicyCampaign(bool includeLateResult,
        bool closeBeforeTypeIDate = false, string audienceKind = PolicyAudience.CoreSecurity,
        IReadOnlyList<string>? audienceTeams = null)
    {
        var policy = CreateApprovedPolicy();
        var version = policy.FindVersion(2)!;
        var subject = new CampaignSubject("policy", PolicyId, version.Identifier,
            version.Content.Title, version.Version, version.ContentSha256);
        var campaign = CreateCampaign(subject, includeLateResult, closeBeforeTypeIDate,
            audienceKind, audienceTeams);
        var reader = new AggregateReader(policy, campaign);
        return await Fixture(campaign, reader, PolicyContext(policy));
    }

    static async Task<FixtureState> TrainingCampaign(bool includeLaterChanges)
    {
        var catalog = new TrainingCatalog(TenantId, ProgramId);
        var first = new TrainingRequirementContent("Security Awareness", "Annual course",
            PolicyAudience.CoreSecurity, null, "manual");
        Assert.True(catalog.Define(Uuid.CreateVersion4(), "TR-SEC", first, Manager,
            new DateTimeOffset(2027, 1, 1, 12, 0, 0, TimeSpan.Zero)).IsSuccess);
        var firstVersion = catalog.Find(RequirementId, 1)!;
        var subject = new CampaignSubject("training", RequirementId, firstVersion.Identifier,
            firstVersion.Content.CourseName, firstVersion.Version, firstVersion.ContentSha256);
        var campaign = CreateCampaign(subject, includeLateResult: includeLaterChanges);
        if (includeLaterChanges)
            Assert.True(catalog.Revise(Uuid.CreateVersion4(), RequirementId, 1,
                first with { CourseName = "Security Awareness v2" }, Manager, AfterTypeIDate)
                .IsSuccess);
        var reader = new AggregateReader(catalog, campaign);
        return await Fixture(campaign, reader, new TypeIBaselineSourceReadContext(Uuid.Empty, []));
    }

    static Policy CreateApprovedPolicy()
    {
        var policy = new Policy(TenantId, PolicyId);
        var createdAt = new DateTimeOffset(2026, 12, 1, 12, 0, 0, TimeSpan.Zero);
        Assert.True(policy.Create(ProgramId, Uuid.CreateVersion4(), "POL-COMMS",
            PolicyContentValue("Access Control Policy v1"), Actor(AuthorId), AuthorId,
            createdAt).IsSuccess);
        Approve(policy, new DateOnly(2027, 1, 1), createdAt.AddMinutes(2));
        Assert.Null(policy.ProposeSuccessor(ProgramId, 1,
            PolicyContentValue("Access Control Policy v2"), Actor(AuthorId), AuthorId,
            new DateTimeOffset(2027, 1, 10, 12, 0, 0, TimeSpan.Zero)));
        Approve(policy, new DateOnly(2027, 2, 1),
            new DateTimeOffset(2027, 1, 12, 12, 0, 0, TimeSpan.Zero), "impact-digest");
        return policy;
    }

    static void Approve(Policy policy, DateOnly effectiveFrom, DateTimeOffset decidedAt,
        string? impactDigest = null)
    {
        var review = Uuid.CreateVersion4();
        Assert.Null(policy.Review(ProgramId, policy.Revision, review, "accept", "Reviewed",
            Actor(ReviewerId), ReviewerId, decidedAt.AddMinutes(-1)));
        Assert.Null(policy.Approve(ProgramId, policy.Revision, Uuid.CreateVersion4(), review,
            effectiveFrom, impactDigest is null, "Approved", impactDigest, Actor(ApproverId),
            ApproverId, decidedAt));
    }

    static PolicyContent PolicyContentValue(string title) => new(title, "Govern access",
        PolicyAudience.CoreSecurity, null, 12, "Review access quarterly.", null,
        "Security lead", []);

    static PolicyDistributionCampaign CreateCampaign(CampaignSubject subject,
        bool includeLateResult, bool closeBeforeTypeIDate = false,
        string audienceKind = PolicyAudience.CoreSecurity, IReadOnlyList<string>? audienceTeams = null)
    {
        var campaign = new PolicyDistributionCampaign(TenantId, CampaignId);
        var roster = new RosterAudience(new Dictionary<Uuid, RosterMatch>
        {
            [Ada] = new RosterMatch(Ada, "Ada", "employee", "Security",
                new DateOnly(2025, 1, 1)),
            [Bob] = new RosterMatch(Bob, "Bob", "employee", "Security",
                new DateOnly(2025, 1, 1)),
        }, new HashSet<Uuid> { Ada, Bob });
        Assert.True(campaign.Launch(ProgramId, subject, audienceKind, audienceTeams ?? [],
            RosterSnapshotId, new string('b', 64), roster, new DateOnly(2027, 4, 15),
            "Complete the required material", Manager, ManagerId, LaunchAt).IsSuccess);
        if (subject.Kind == "policy")
        {
            Assert.Null(campaign.Acknowledge(ProgramId, AdaAcknowledgementId, Ada,
                subject.Version, subject.ContentSha256,
                PolicyDistributionCampaign.DefaultAcknowledgementText,
                Actor(Ada), Manager, true, new DateTimeOffset(2027, 3, 1, 12, 0, 0,
                    TimeSpan.Zero)));
            if (includeLateResult)
                Assert.Null(campaign.Acknowledge(ProgramId, BobAcknowledgementId, Bob,
                    subject.Version, subject.ContentSha256,
                    PolicyDistributionCampaign.DefaultAcknowledgementText,
                    Actor(Bob), Manager, true, AfterTypeIDate));
        }
        else
        {
            Assert.Null(campaign.RecordCompletion(ProgramId, AdaCompletionId, Ada,
                subject.Version, new DateOnly(2027, 3, 1), "manual", "certificate-a.pdf",
                Manager, new DateTimeOffset(2027, 3, 1, 12, 0, 0, TimeSpan.Zero)));
            if (includeLateResult)
                Assert.Null(campaign.RecordCompletion(ProgramId, BobCompletionId, Bob,
                    subject.Version, new DateOnly(2027, 4, 1), "manual", "certificate-b.pdf",
                    Manager, AfterTypeIDate));
        }
        if (closeBeforeTypeIDate)
            Assert.Null(campaign.Close(ProgramId, "The communication review is complete.",
                Manager, new DateTimeOffset(2027, 3, 20, 12, 0, 0, TimeSpan.Zero)));
        return campaign;
    }

    static async Task<FixtureState> Fixture(PolicyDistributionCampaign campaign,
        IAggregateReader reader, TypeIBaselineSourceReadContext context)
    {
        var pending = new AggregateScenario<PolicyDistributionCampaign>(campaign).PendingEvents;
        var attached = pending.ToArray();
        var events = new InMemoryEventStore();
        var stream = new EventStreamAddress(TenantId.ToString(), "policy-distribution-campaigns",
            campaign.Id.ToString());
        await events.AppendAsync(stream, 0, attached);
        var checkpoint = await CheckpointAfterAsync(events);
        var replayed = new AggregateScenario<PolicyDistributionCampaign>(
            new PolicyDistributionCampaign(TenantId, campaign.Id)).Given(attached).Aggregate;
        var directory = new CampaignDirectory(replayed, checkpoint);
        var contributor = new PolicyCommunicationTypeIBaselineSourceContributor(directory,
            reader, events, new CampaignDirectoryReadConsistency(directory, events));
        return new FixtureState(contributor, context);
    }

    static async Task<ProjectionCheckpoint> CheckpointAfterAsync(InMemoryEventStore events)
    {
        var cursor = EventCursor.Start;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(
                           TenantId.ToString(), "policy-distribution-campaigns"), cursor,
                           CancellationToken.None))
            cursor = record.NextCursor;
        return new ProjectionCheckpoint(cursor);
    }

    static ActorReference Actor(Uuid memberId) => ActorReference.ForMember(memberId,
        memberId.ToString());

    sealed record FixtureState(ITypeIBaselineSourceContributor Contributor,
        TypeIBaselineSourceReadContext Context);

    sealed class CampaignDirectory(PolicyDistributionCampaign campaign,
        ProjectionCheckpoint checkpoint) : ICampaignDirectoryReader
    {
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(checkpoint);

        public ValueTask<Page<CampaignSummaryView>> ListProgramAsync(Uuid tenantId,
            Uuid programId, int limit, string? cursor, CancellationToken ct = default)
        {
            var view = campaign.ToView(TypeIDate);
            var item = new CampaignSummaryView(TenantId, ProgramId, campaign.Id,
                view.Subject.Kind, view.Subject.RecordId, view.Subject.Identifier,
                view.Subject.Version, view.Subject.Title, view.DueOn, view.Status,
                view.Totals.LaunchAudience, view.LaunchedAt);
            return ValueTask.FromResult(new Page<CampaignSummaryView>([item], null));
        }

        public ValueTask<IReadOnlyList<Uuid>> ListForSubjectAsync(Uuid tenantId,
            Uuid programId, Uuid subjectId, long version, CancellationToken ct = default) =>
            ValueTask.FromResult<IReadOnlyList<Uuid>>([]);
    }

    sealed class AggregateReader(params Aggregate[] aggregates) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult(aggregates.FirstOrDefault(item => item.Id == aggregate.Id &&
                item.GetType() == typeof(TAggregate)) is TAggregate found ? found : aggregate);
    }
}
