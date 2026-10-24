using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.Work;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Examinations;

public sealed class RiskTypeIBaselineSourceContributorTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid RiskId = RiskDraft.IdFor(TenantId, ProgramId, "R-01");
    static readonly Uuid ActorMemberId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerMemberId = Uuid.CreateVersion4();
    static readonly Uuid ControlId = Uuid.CreateVersion4();
    static readonly Uuid ControlVersionId = Uuid.CreateVersion4();
    static readonly DateOnly TypeIDate = new(2027, 3, 31);
    static readonly DateTimeOffset CreatedAt = new(2027, 2, 1, 12, 0, 0,
        TimeSpan.Zero);
    static readonly DateTimeOffset RevisedAfterTypeI = new(2027, 4, 1, 12, 0, 0,
        TimeSpan.Zero);

    [Fact]
    public async Task ShouldCaptureRiskAtTypeIDateGivenLaterDraftRevision()
    {
        // Arrange
        await using var fixture = await Fixture();

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("risks", result.Value.Family);
        var source = Assert.Single(result.Value.Sources);
        Assert.Equal("risk", source.Kind);
        Assert.Equal(RiskId, source.RecordId);
        Assert.Equal(1, source.Revision);
        Assert.Equal(Uuid.CreateVersion5(RiskId, "risk_v1:2027-03-31"), source.VersionId);
        Assert.Equal(64, source.ContentSha256.Length);
    }

    [Fact]
    public async Task ShouldCaptureAssessmentMethodAndOwnerGivenCompleteRiskAtTypeIDate()
    {
        // Arrange
        await using var fixture = await Fixture(includeAssessmentAndOwner: true);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Single(result.Value.Sources);
        Assert.Empty(result.Value.Blockers);
        Assert.Empty(result.Value.ScopeDifferences);
    }

    [Fact]
    public async Task ShouldExcludeFutureDatedAssessmentGivenItWasRecordedBeforeTypeIDate()
    {
        // Arrange
        await using var fixture = await Fixture(includeAssessmentAndOwner: true,
            assessmentAt: RevisedAfterTypeI, assessmentRecordedAt: CreatedAt);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Contains(result.Value.Blockers, blocker =>
            blocker.Contains("no inherent assessment", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShouldExcludeBackdatedAssessmentGivenItWasRecordedAfterTypeIDate()
    {
        // Arrange
        await using var fixture = await Fixture(includeAssessmentAndOwner: true,
            assessmentAt: CreatedAt, assessmentRecordedAt: RevisedAfterTypeI);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Contains(result.Value.Blockers, blocker =>
            blocker.Contains("no inherent assessment", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenEvaluationDirectoryBehindEvents()
    {
        // Arrange
        await using var fixture = await Fixture(includeAssessmentAndOwner: true,
            evaluationDirectoryCaughtUp: false);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
        Assert.True(result.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldRequireAcceptedControlTreatmentToMatchCapturedControlVersionGivenMitigate()
    {
        // Arrange
        await using var fixture = await Fixture(includeAssessmentAndOwner: true,
            includeAcceptedControlTreatment: true);
        var controlVersion = new TypeIBaselineSourceReference("control_version", ControlId,
            ControlVersionId, 1, new string('a', 64));
        var context = new TypeIBaselineSourceReadContext(Uuid.Empty, [controlVersion]);

        // Act
        var missingControl = await fixture.Contributor.ReadAsync(Engagement(),
            new TypeIBaselineSourceReadContext(Uuid.Empty, []), CancellationToken.None);
        var capturedControl = await fixture.Contributor.ReadAsync(Engagement(), context,
            CancellationToken.None);

        // Assert
        Assert.True(missingControl.IsSuccess, missingControl.Error?.Message);
        Assert.Contains(missingControl.Value.Blockers, blocker =>
            blocker.Contains("accepted control treatment", StringComparison.Ordinal));
        Assert.True(capturedControl.IsSuccess, capturedControl.Error?.Message);
        Assert.DoesNotContain(capturedControl.Value.Blockers, blocker =>
            blocker.Contains("accepted control treatment", StringComparison.Ordinal));
    }

    static ExaminationEngagementView Engagement() => new(TenantId, ProgramId,
        Uuid.CreateVersion4(), "type_i", "Audit Firm", TypeIDate, null, ["security"],
        Uuid.CreateVersion4(), [], "draft", 1,
        new DateTimeOffset(TypeIDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc)),
        ActorMemberId, "Examiner");

    static async Task<FixtureState> Fixture(bool includeAssessmentAndOwner = false,
        DateTimeOffset? assessmentAt = null, DateTimeOffset? assessmentRecordedAt = null,
        bool evaluationDirectoryCaughtUp = true, bool includeAcceptedControlTreatment = false)
    {
        var risk = new RiskDraft(TenantId, RiskId);
        Assert.True(risk.Create(ProgramId, Uuid.CreateVersion4(), "R-01",
            new RiskDraftContent("Credential misuse", "A credential may be misused.",
                "Unauthorized access to customer data.", "Security review."), ActorMemberId,
            "Risk owner", CreatedAt).IsSuccess);
        Assert.Null(risk.Revise(ProgramId, 1,
            new RiskDraftContent("Revised credential misuse", "A credential may be misused.",
                "Unauthorized access to customer data.", "Updated after the audit date."),
            ActorMemberId, "Risk owner", RevisedAfterTypeI));

        var method = new RiskMethod(TenantId, ProgramId);
        var evaluation = new RiskEvaluation(TenantId, RiskId);
        if (includeAssessmentAndOwner)
        {
            var actor = ActorReference.ForMember(ActorMemberId, "Risk owner");
            Assert.Null(method.Publish(ProgramId, 0, ["Rare", "Unlikely", "Possible",
                "Likely", "Almost certain"], ["Negligible", "Minor", "Moderate", "Major",
                "Severe"], 12, actor, CreatedAt));
            Assert.Null(evaluation.RecordAssessment(ProgramId, 0, Uuid.CreateVersion4(),
                method.Current!, RiskEvaluation.Inherent, 3, 4,
                "A compromised credential could expose customer records.", ActorMemberId,
                "Risk owner", assessmentAt ?? CreatedAt));
            if (assessmentRecordedAt is null)
                Assert.Null(evaluation.ChooseTreatment(ProgramId, 1, "mitigate",
                    "Limit credential exposure.", ActorMemberId, "Risk owner",
                    CreatedAt.AddHours(1)));
        }

        var store = new InMemoryEventStore();
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(store);
        services.AddSingleton<IDomainEventReader>(store);
        services.AddSingleton<IImmutableContentStore>(new FitzKvImmutableContentStore(new InMemoryKvClient()));
        services.AddPortia();
        var provider = services.BuildServiceProvider();
        var reader = provider.GetRequiredService<IAggregateReader>();
        var content = provider.GetRequiredService<IImmutableContentStore>();
        if (includeAssessmentAndOwner)
        {
            var actor = ActorReference.ForMember(ActorMemberId, "Risk owner");
            await RiskSeeds.AssignOwnerAsync(provider, TenantId, ProgramId, RiskId, Uuid.CreateVersion4(),
                ActorMemberId, "Risk owner is accountable for the mitigation.", actor, CreatedAt);
            if (includeAcceptedControlTreatment)
            {
                var treatmentId = Uuid.CreateVersion4();
                await RiskSeeds.ProposeTreatmentAsync(provider, TenantId, ProgramId, RiskId, treatmentId,
                    ControlId, ControlVersionId, "The approved control addresses this credential risk.",
                    ActorMemberId, actor, CreatedAt);
                await RiskSeeds.ReviewTreatmentAsync(provider, TenantId, ProgramId, RiskId, ControlId,
                    treatmentId, "accept", "The control treatment is supported.",
                    ActorReference.ForMember(ReviewerMemberId, "Reviewer"), CreatedAt.AddHours(2));
            }
        }
        var stream = new EventStreamAddress(TenantId.ToString(), "risks", RiskId.ToString());
        await store.AppendAsync(stream, 0, new AggregateScenario<RiskDraft>(risk).PendingEvents
            .ToArray());
        var draftDirectory = new RiskDraftDirectory(Summary(risk),
            await CheckpointAfterAsync(store, EventStreamPattern.ForPattern(TenantId.ToString(),
                "risks")));
        if (method.PublishedVersion > 0)
            await store.AppendAsync(new EventStreamAddress(TenantId.ToString(), "risk-methods",
                method.Id.ToString()), 0,
                new AggregateScenario<RiskMethod>(method).PendingEvents.ToArray());
        if (evaluation.Revision > 0)
        {
            var evaluationEvents = new AggregateScenario<RiskEvaluation>(evaluation)
                .PendingEvents.ToArray();
            if (assessmentRecordedAt is { } recordedAt &&
                evaluationEvents[0] is RiskAssessmentRecorded recorded)
            {
                DomainEvent stamped = new RiskAssessmentRecorded(recorded.TenantId,
                    recorded.ProgramId, recorded.RiskId, recorded.Revision, recorded.Assessment);
                stamped.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), RiskId, 1,
                    recordedAt));
                evaluationEvents[0] = stamped;
            }
            await store.AppendAsync(new EventStreamAddress(TenantId.ToString(), "risk-evaluations",
                RiskId.ToString()), 0, evaluationEvents);
        }
        var evaluationCheckpoint = evaluationDirectoryCaughtUp
            ? await CheckpointAfterAsync(store, EventStreamPattern.ForPattern(TenantId.ToString(),
                "risk-evaluations"))
            : new ProjectionCheckpoint(EventCursor.Start);
        var evaluationDirectory = new RiskEvaluationDirectory(evaluationCheckpoint,
            evaluation.Revision > 0 ? evaluation.ToView() : null);
        return new FixtureState(new RiskTypeIBaselineSourceContributor(draftDirectory,
            evaluationDirectory, new RiskGovernanceReader(reader, store, content), content, reader, store), provider);
    }

    static RiskDraftView Summary(RiskDraft risk) => new(TenantId, ProgramId, RiskId,
        "R-01", risk.Revision, "draft_unassessed", "unresolved",
        new RiskDraftContent("Revised credential misuse", "A credential may be misused.",
            "Unauthorized access to customer data.", "Updated after the audit date."),
        ActorMemberId, "Risk owner", RevisedAfterTypeI);

    static async Task<ProjectionCheckpoint> CheckpointAfterAsync(InMemoryEventStore store,
        EventStreamPattern pattern)
    {
        var cursor = EventCursor.Start;
        await foreach (var record in store.ReadAsync(pattern, cursor, CancellationToken.None))
            cursor = record.NextCursor;
        return new ProjectionCheckpoint(cursor);
    }

    sealed record FixtureState(ITypeIBaselineSourceContributor Contributor, ServiceProvider Provider) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Provider.DisposeAsync();
    }

    sealed class RiskDraftDirectory(RiskDraftView summary, ProjectionCheckpoint checkpoint)
        : IRiskDraftDirectoryReader
    {
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(checkpoint);

        public ValueTask<RiskDraftView?> GetAsync(Uuid tenantId, Uuid riskId,
            CancellationToken ct = default) => ValueTask.FromResult<RiskDraftView?>(summary);

        public ValueTask<Page<RiskDraftView>> ListProgramAsync(Uuid tenantId, Uuid programId,
            int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<RiskDraftView>([summary], null));

        public ValueTask<RiskDraftRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid riskId,
            long revision, CancellationToken ct = default) =>
            ValueTask.FromResult<RiskDraftRevisionView?>(null);
    }

    sealed class RiskEvaluationDirectory(ProjectionCheckpoint checkpoint,
        RiskEvaluationView? current) : IRiskEvaluationDirectoryReader
    {
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(checkpoint);

        public ValueTask<RiskEvaluationView?> GetAsync(Uuid tenantId, Uuid riskId,
            CancellationToken ct = default) => ValueTask.FromResult(current);

        public ValueTask<Page<RiskEvaluationHistoryEntryView>> ListHistoryAsync(Uuid tenantId,
            Uuid riskId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<RiskEvaluationHistoryEntryView>([], null));
    }

}
