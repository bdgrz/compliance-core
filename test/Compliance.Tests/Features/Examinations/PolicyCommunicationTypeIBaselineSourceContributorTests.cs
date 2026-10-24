using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Examinations;

public sealed class PolicyCommunicationTypeIBaselineSourceContributorTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid PolicyId = Policy.IdFor(TenantId, ProgramId, "POL-COMMS");
    static readonly Uuid RequirementId = TrainingRequirementIdentity.IdFor(TenantId, ProgramId, "TR-SEC");
    static readonly Uuid CampaignId = Uuid.CreateVersion4();
    static readonly Uuid RosterSnapshotId = Uuid.CreateVersion4();
    static readonly Uuid Ada = Uuid.CreateVersion4();
    static readonly Uuid Bob = Uuid.CreateVersion4();
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
        await using var fixture = await PolicyCampaign();
        var original = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context, CancellationToken.None);
        await fixture.AcknowledgeAsync(Bob, AfterTypeIDate);

        // Act
        var later = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context, CancellationToken.None);
        var direct = await fixture.Contributor.ReadAsync(Engagement(), CancellationToken.None);

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
        await using var fixture = await PolicyCampaign();
        await CampaignSeed.CloseAsync(fixture.Provider, TenantId, CampaignId, "The communication review is complete.",
            Manager, new DateTimeOffset(2027, 3, 20, 12, 0, 0, TimeSpan.Zero));

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context, CancellationToken.None);

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
        await using var fixture = await PolicyCampaign(audienceKind: PolicyAudience.RoleTargeted,
            audienceTeams: ["Finance"]);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
    }

    [Fact]
    public async Task ShouldCaptureTrainingVersionAsOfTypeIDateGivenLaterRequirementAndCompletion()
    {
        // Arrange
        await using var fixture = await TrainingCampaign();

        // Act
        var original = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context, CancellationToken.None);
        await TrainingSeed.ReviseAsync(fixture.Provider, TenantId, RequirementId, "TR-SEC", 1,
            new TrainingRequirementContent("Security Awareness v2", "Annual course", PolicyAudience.CoreSecurity, null,
                "manual"), Manager, AfterTypeIDate);
        var completed = await CampaignSeed.CompleteAsync(fixture.Provider, TenantId, ProgramId, CampaignId, Bob, 1,
            new DateOnly(2027, 4, 1), "manual", "certificate-b.pdf", Manager, AfterTypeIDate);
        var later = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context, CancellationToken.None);

        // Assert
        Assert.True(completed.IsSuccess, completed.Error?.Message);
        Assert.True(original.IsSuccess, original.Error?.Message);
        Assert.True(later.IsSuccess, later.Error?.Message);
        Assert.Equal("policy_communications", original.Value.Family);
        var requirement = Assert.Single(original.Value.Sources, item => item.Kind == "training_requirement_version");
        Assert.Equal(RequirementId, requirement.RecordId);
        Assert.Equal(Uuid.CreateVersion5(RequirementId, "training_requirement_version_v1:1"), requirement.VersionId);
        var campaign = Assert.Single(original.Value.Sources, item => item.Kind == "policy_communication");
        var laterCampaign = Assert.Single(later.Value.Sources, item => item.Kind == "policy_communication");
        Assert.Equal(campaign.ContentSha256, laterCampaign.ContentSha256);
        Assert.Equal(2, later.Value.Sources.Count);
        Assert.Empty(later.Value.Blockers);
    }

    static ExaminationEngagementView Engagement() => new(TenantId, ProgramId,
        Uuid.CreateVersion4(), "type_i", "Audit Firm", TypeIDate, null, ["security"],
        Uuid.CreateVersion4(), [], "draft", 1, LaunchAt, ManagerId, "Examiner");

    static TypeIBaselineSourceReadContext PolicyContext(PolicyReadSnapshot policy)
    {
        var references = PolicyTypeIBaselineSourceContributor.ReferencesAsOf(TenantId, policy,
            TypeIDate, new DateTimeOffset(TypeIDate.ToDateTime(TimeOnly.MaxValue,
                DateTimeKind.Utc)), out var valid);
        Assert.True(valid);
        return new TypeIBaselineSourceReadContext(Uuid.CreateVersion4(), references);
    }

    static async Task<Fixture> PolicyCampaign(string audienceKind = PolicyAudience.CoreSecurity,
        IReadOnlyList<string>? audienceTeams = null)
    {
        var fixture = new Fixture();
        var policy = CreateApprovedPolicy();
        await fixture.SaveAsync(policy);
        var version = policy.FindVersion(2)!;
        var subject = new CampaignSubject("policy", PolicyId, version.Identifier, version.Content.Title,
            version.Version, version.ContentSha256);
        await fixture.LaunchAsync(subject, audienceKind, audienceTeams ?? []);
        fixture.Subject = subject;
        fixture.Context = PolicyContext(await fixture.PolicyAsync());
        var acknowledged = await fixture.AcknowledgeAsync(Ada, new DateTimeOffset(2027, 3, 1, 12, 0, 0, TimeSpan.Zero));
        Assert.True(acknowledged.IsSuccess, acknowledged.Error?.Message);
        return fixture;
    }

    static async Task<Fixture> TrainingCampaign()
    {
        var fixture = new Fixture();
        var first = new TrainingRequirementContent("Security Awareness", "Annual course",
            PolicyAudience.CoreSecurity, null, "manual");
        await TrainingSeed.DefineAsync(fixture.Provider, TenantId, ProgramId, "TR-SEC", first, Manager,
            new DateTimeOffset(2027, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var subject = new CampaignSubject("training", RequirementId, "TR-SEC", first.CourseName, 1,
            TrainingRequirementRules.Hash(TrainingRequirementRules.Clean(first)));
        await fixture.LaunchAsync(subject, PolicyAudience.CoreSecurity, []);
        fixture.Subject = subject;
        fixture.Context = new TypeIBaselineSourceReadContext(Uuid.Empty, []);
        var completed = await CampaignSeed.CompleteAsync(fixture.Provider, TenantId, ProgramId, CampaignId, Ada, 1,
            new DateOnly(2027, 3, 1), "manual", "certificate-a.pdf", Manager,
            new DateTimeOffset(2027, 3, 1, 12, 0, 0, TimeSpan.Zero));
        Assert.True(completed.IsSuccess, completed.Error?.Message);
        return fixture;
    }

    static PolicyRules CreateApprovedPolicy()
    {
        var policy = new PolicyRules(TenantId, PolicyId);
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

    static void Approve(PolicyRules policy, DateOnly effectiveFrom, DateTimeOffset decidedAt,
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

    static ActorReference Actor(Uuid memberId) => ActorReference.ForMember(memberId,
        memberId.ToString());

    /// <summary>Owners, projection fence and contributor composed over one in-memory store.</summary>
    sealed class Fixture : IAsyncDisposable
    {
        readonly InMemoryEventStore _events = new();

        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(_events);
            services.AddSingleton<IDomainEventReader>(_events);
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<IImmutableContentStore>(new FitzKvImmutableContentStore(
                new PersistenceKvClient(new InMemoryKvClient())));
            services.AddPortia();
            Provider = services.BuildServiceProvider();
        }

        public ServiceProvider Provider { get; }

        public CampaignSubject Subject { get; set; } = null!;

        public TypeIBaselineSourceReadContext Context { get; set; } = null!;

        public ITypeIBaselineSourceContributor Contributor
        {
            get
            {
                var scope = Provider.CreateScope();
                var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
                var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
                var campaigns = new CampaignSource(reader, content);
                var directory = new CampaignDirectory(campaigns, _events);
                return new PolicyCommunicationTypeIBaselineSourceContributor(directory, new PolicyReader(reader, _events, content), _events,
                    new CampaignDirectoryReadConsistency(directory, _events), campaigns, content);
            }
        }

        public async Task<PolicyReadSnapshot> PolicyAsync()
        {
            await using var scope = Provider.CreateAsyncScope();
            var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
            return await new PolicyReader(reader, _events, content).ReadAsync(TenantId, ProgramId, PolicyId);
        }

        public async Task SaveAsync(PolicyRules rules)
        {
            await using var scope = Provider.CreateAsyncScope();
            await PolicySeeds.AppendAsync(scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
                scope.ServiceProvider.GetRequiredService<IAggregateWriter>(),
                scope.ServiceProvider.GetRequiredService<IImmutableContentStore>(), rules);
        }

        public async Task SaveAsync(Aggregate aggregate)
        {
            await using var scope = Provider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IAggregateWriter>().SaveAsync(aggregate,
                new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        }

        public Task<PolicyDistributionCampaign> LaunchAsync(CampaignSubject subject, string audienceKind, IReadOnlyList<string> teams)
        {
            var roster = new RosterAudience(new Dictionary<Uuid, RosterMatch>
            {
                [Ada] = new(Ada, "Ada", "employee", "Security", new DateOnly(2025, 1, 1)),
                [Bob] = new(Bob, "Bob", "employee", "Security", new DateOnly(2025, 1, 1)),
            }, new HashSet<Uuid> { Ada, Bob });
            return CampaignSeed.LaunchAsync(Provider, TenantId, ProgramId, CampaignId, subject, audienceKind, teams,
                RosterSnapshotId, new string('b', 64), roster, new DateOnly(2027, 4, 15),
                "Complete the required material", Manager, ManagerId, LaunchAt);
        }

        public Task<Result<ReceiverOutcome>> AcknowledgeAsync(Uuid person, DateTimeOffset at) =>
            CampaignSeed.AcknowledgeAsync(Provider, TenantId, ProgramId, CampaignId, person, Subject.Version,
                Subject.ContentSha256, Manager, Actor(person), true, at);

        public ValueTask DisposeAsync() => Provider.DisposeAsync();
    }

    sealed class CampaignDirectory(CampaignSource campaigns, InMemoryEventStore events) : ICampaignDirectoryReader
    {
        public async ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default)
        {
            var cursor = EventCursor.Start;
            await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(),
                               "policy-distribution-campaigns"), cursor, ct))
                cursor = record.NextCursor;
            return new ProjectionCheckpoint(cursor);
        }

        public async ValueTask<Page<CampaignSummaryView>> ListProgramAsync(Uuid tenantId, Uuid programId, int limit,
            string? cursor, CancellationToken ct = default)
        {
            var state = (await campaigns.ReadAsync(TenantId, ProgramId, CampaignId, ct)).Value;
            var view = state.ToView(TypeIDate);
            var item = new CampaignSummaryView(TenantId, ProgramId, CampaignId, view.Subject.Kind,
                view.Subject.RecordId, view.Subject.Identifier, view.Subject.Version, view.Subject.Title, view.DueOn,
                view.Status == "closed" ? "closed" : "open", view.Totals.LaunchAudience, view.LaunchedAt);
            return new Page<CampaignSummaryView>([item], null);
        }

        public ValueTask<IReadOnlyList<Uuid>> ListForSubjectAsync(Uuid tenantId, Uuid programId, Uuid subjectId,
            long version, CancellationToken ct = default) => ValueTask.FromResult<IReadOnlyList<Uuid>>([]);
    }
}
