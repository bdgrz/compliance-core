using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.SystemDescription;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using DescriptionAggregate = Bdgrz.Compliance.Features.SystemDescription.SystemDescription;
using TypeIIPlanAggregate = Bdgrz.Compliance.Features.Examinations.TypeIIPlan;
using TypeIIPeriodAggregate = Bdgrz.Compliance.Features.Operations.TypeIIPeriod;

namespace Bdgrz.Compliance.Tests.Features.SystemDescription;

public sealed class SystemDescriptionDraftCommandHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 17, 21, 0, 0, TimeSpan.Zero);
    static readonly string[] SectionIds =
    [
        "services", "commitments_requirements", "components_infrastructure_software",
        "people", "procedures", "information_data_flows", "system_boundary",
        "control_environment", "user_entity_controls", "subservice_organizations",
        "exclusions", "relevant_changes",
    ];

    [Fact]
    public async Task ShouldCreateDraftFromFrozenBaselineWithExactSourcesGivenPersonalCommand()
    {
        // Arrange
        await using var fixture = new Fixture();
        var baseline = await fixture.SeedBaselineAsync();
        var request = new CreateSystemDescriptionDraft(fixture.TenantId, fixture.ProgramId,
            fixture.DescriptionId, fixture.ExaminationId, "The Sample System", Sections());

        // Act
        var result = await fixture.CreateHandler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new DescriptionAggregate(fixture.TenantId,
            fixture.ProgramId, fixture.DescriptionId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("draft", saved.CurrentVersion!.Status);
        Assert.Equal("type_i", saved.CurrentVersion.Scope.Stage);
        Assert.Equal(fixture.BaselineId, saved.CurrentVersion.Scope.TypeIBaselineId);
        Assert.Equal("The Sample System", saved.CurrentVersion.Content.Title);
        Assert.Equal(SectionIds.OrderBy(static id => id, StringComparer.Ordinal),
            saved.CurrentVersion.Content.Sections.Select(item => item.SectionId));
        var source = Assert.Single(saved.CurrentVersion.Content.Sources);
        Assert.Equal(fixture.BoundaryId, source.RecordId);
        Assert.Equal(baseline.Sources[0].VersionId, source.VersionId);
        Assert.Equal(baseline.Sources[0].ContentSha256, source.ContentSha256);
        Assert.Equal("current", source.Status);
    }

    [Fact]
    public async Task ShouldRejectDraftCreationWithoutFrozenBaselineGivenPersonalCommand()
    {
        // Arrange
        await using var fixture = new Fixture();
        var request = new CreateSystemDescriptionDraft(fixture.TenantId, fixture.ProgramId,
            fixture.DescriptionId, fixture.ExaminationId, "The Sample System", Sections());

        // Act
        var result = await fixture.CreateHandler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new DescriptionAggregate(fixture.TenantId,
            fixture.ProgramId, fixture.DescriptionId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Null(saved.CurrentVersion);
    }

    [Fact]
    public async Task ShouldRequirePersonalHttpActorGivenDraftCreationCommand()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedBaselineAsync();
        var request = new CreateSystemDescriptionDraft(fixture.TenantId, fixture.ProgramId,
            fixture.DescriptionId, fixture.ExaminationId, "The Sample System", Sections());

        // Act
        var result = await fixture.CreateHandler().HandleAsync(Fixture.SystemContext(request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new DescriptionAggregate(fixture.TenantId,
            fixture.ProgramId, fixture.DescriptionId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Null(saved.CurrentVersion);
    }

    [Fact]
    public async Task ShouldReviseDraftContentAtExpectedRevisionGivenPersonalCommand()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedBaselineAsync();
        await fixture.SeedDescriptionAsync();
        var sections = Sections().Select(item => item with
        {
            Content = item.SectionId == "services" ? "Updated service description." : item.Content,
        }).ToArray();
        var request = new ReviseSystemDescriptionDraft(fixture.TenantId, fixture.ProgramId,
            fixture.DescriptionId, 1, "Updated System", sections, "Clarified service scope.");

        // Act
        var result = await fixture.ReviseHandler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new DescriptionAggregate(fixture.TenantId,
            fixture.ProgramId, fixture.DescriptionId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(2, saved.Revision);
        Assert.Equal("Updated System", saved.CurrentVersion!.Content.Title);
        Assert.Equal("Updated service description.", saved.CurrentVersion.Content.Sections
            .Single(item => item.SectionId == "services").Content);
        Assert.Equal(fixture.UserMemberId, saved.CurrentVersion.AuthorMemberId);
        Assert.Equal(fixture.BaselineSourceVersionId, Assert.Single(saved.CurrentVersion.Content.Sources).VersionId);
    }

    [Fact]
    public async Task ShouldRejectStaleDraftRevisionWithoutAppendingGivenPersonalCommand()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedBaselineAsync();
        await fixture.SeedDescriptionAsync();
        var request = new ReviseSystemDescriptionDraft(fixture.TenantId, fixture.ProgramId,
            fixture.DescriptionId, 0, "Stale System", Sections(), "Stale edit.");

        // Act
        var result = await fixture.ReviseHandler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new DescriptionAggregate(fixture.TenantId,
            fixture.ProgramId, fixture.DescriptionId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(1, saved.Revision);
        Assert.Equal("The Sample System", saved.CurrentVersion!.Content.Title);
    }

    [Fact]
    public async Task ShouldBindOnlyExactBaselineVersionsGivenPersonalCommand()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedBaselineAsync();
        await fixture.SeedDescriptionAsync();
        var request = new BindSystemDescriptionSources(fixture.TenantId, fixture.ProgramId,
            fixture.DescriptionId, 1, [fixture.BaselineSourceVersionId]);

        // Act
        var result = await fixture.BindSourcesHandler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new DescriptionAggregate(fixture.TenantId,
            fixture.ProgramId, fixture.DescriptionId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(2, saved.Revision);
        var source = Assert.Single(saved.CurrentVersion!.Content.Sources);
        Assert.Equal(fixture.BoundaryId, source.RecordId);
        Assert.Equal(fixture.BaselineSourceVersionId, source.VersionId);
    }

    [Fact]
    public async Task ShouldRejectUnboundSourceVersionWithoutChangingDescriptionGivenPersonalCommand()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedBaselineAsync();
        await fixture.SeedDescriptionAsync();
        var request = new BindSystemDescriptionSources(fixture.TenantId, fixture.ProgramId,
            fixture.DescriptionId, 1, [Uuid.CreateVersion4()]);

        // Act
        var result = await fixture.BindSourcesHandler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new DescriptionAggregate(fixture.TenantId,
            fixture.ProgramId, fixture.DescriptionId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(1, saved.Revision);
    }

    [Fact]
    public async Task ShouldRejectPartialBaselineSourceSetWithoutChangingDescriptionGivenPersonalCommand()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedBaselineAsync(includeAdditionalSource: true);
        await fixture.SeedDescriptionAsync();
        var request = new BindSystemDescriptionSources(fixture.TenantId, fixture.ProgramId,
            fixture.DescriptionId, 1, [fixture.BaselineSourceVersionId]);

        // Act
        var result = await fixture.BindSourcesHandler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new DescriptionAggregate(fixture.TenantId,
            fixture.ProgramId, fixture.DescriptionId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(1, saved.Revision);
    }

    [Fact]
    public async Task ShouldBindTypeIIDescriptionToItsExactClosedPeriodSourcesGivenPersonalCommand()
    {
        // Arrange
        await using var fixture = new Fixture();
        var period = await fixture.SeedClosedTypeIIPeriodAsync();
        await fixture.SeedTypeIIDescriptionAsync(period.CloseSnapshots[^1].CloseSnapshotId);
        var sourceVersionIds = period.CloseSnapshots[^1].SourceReferences
            .Select(static source => source.VersionId).ToArray();
        var request = new BindSystemDescriptionSources(fixture.TenantId, fixture.ProgramId,
            fixture.DescriptionId, 1, sourceVersionIds);

        // Act
        var result = await fixture.BindSourcesHandler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new DescriptionAggregate(fixture.TenantId,
            fixture.ProgramId, fixture.DescriptionId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(2, saved.Revision);
        Assert.Equal(sourceVersionIds.OrderBy(static id => id.ToString(), StringComparer.Ordinal),
            saved.CurrentVersion!.Content.Sources.Select(static source => source.VersionId)
                .OrderBy(static id => id.ToString(), StringComparer.Ordinal));
        Assert.All(saved.CurrentVersion.Content.Sources, static source =>
            Assert.Equal("current", source.Status));
    }

    [Fact]
    public async Task ShouldRejectTypeIIDescriptionGivenSupersededCloseSnapshot()
    {
        // Arrange
        await using var fixture = new Fixture();
        var period = await fixture.SeedClosedTypeIIPeriodAsync();
        await fixture.SeedTypeIIDescriptionAsync(Uuid.CreateVersion4());
        var request = new BindSystemDescriptionSources(fixture.TenantId, fixture.ProgramId,
            fixture.DescriptionId, 1, period.CloseSnapshots[^1].SourceReferences
                .Select(static source => source.VersionId).ToArray());

        // Act
        var result = await fixture.BindSourcesHandler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new DescriptionAggregate(fixture.TenantId,
            fixture.ProgramId, fixture.DescriptionId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(1, saved.Revision);
    }

    static SystemDescriptionSection[] Sections() => SectionIds.Select(id =>
        new SystemDescriptionSection(id, id.Replace('_', ' '), "Current system fact.",
            "complete", null)).ToArray();

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly TimeProvider _clock = new FixedTimeProvider(Now);
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid ExaminationId { get; } = Uuid.CreateVersion4();
        public Uuid DescriptionId { get; } = Uuid.CreateVersion4();
        public Uuid BaselineId { get; } = Uuid.CreateVersion4();
        public Uuid PlanId { get; } = Uuid.CreateVersion4();
        public Uuid PeriodId { get; } = Uuid.CreateVersion4();
        public Uuid CloseSnapshotId { get; } = Uuid.CreateVersion4();
        public Uuid BoundaryId { get; } = Uuid.CreateVersion4();
        public Uuid BaselineSourceVersionId { get; } = Uuid.CreateVersion4();
        public Uuid AdditionalSourceRecordId { get; } = Uuid.CreateVersion4();
        public Uuid AdditionalSourceVersionId { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public Uuid ReviewerId { get; } = Uuid.CreateVersion4();
        public Uuid ApproverId { get; } = Uuid.CreateVersion4();
        public Uuid ValidatorId { get; } = Uuid.CreateVersion4();
        public Uuid GapId { get; } = Uuid.CreateVersion4();
        public Uuid ControlId { get; } = Uuid.CreateVersion4();
        public Uuid ControlVersionId { get; } = Uuid.CreateVersion4();
        public Uuid UserMemberId => RbacIds.Member(TenantId, UserId);
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }
        public IAggregateExecutor Executor { get; }

        ClaimsPrincipal Actor => new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", UserId.ToString()),
                new Claim("email", "author@example.test")], "BdgrzSession"));

        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddPortia();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        }

        public CreateSystemDescriptionDraftHandler CreateHandler() => new(Reader, Executor, _clock);

        public ReviseSystemDescriptionDraftHandler ReviseHandler() => new(Executor, _clock);

        public BindSystemDescriptionSourcesHandler BindSourcesHandler() => new(Reader, Executor, _clock);

        public Cntryl.Portia.RequestContext<T> Context<T>(T request) where T : IRequestBase => new(request,
            new RequestDispatchContext(Actor, new HttpInvocation("POST", "/system-descriptions",
                "/system-descriptions", "system-description-draft-test")));

        public static Cntryl.Portia.RequestContext<T> SystemContext<T>(T request) where T : IRequestBase =>
            new(request, new RequestDispatchContext(RequestActor.System,
                new HttpInvocation("POST", "/system-descriptions", "/system-descriptions",
                    "system-description-draft-test")));

        public async Task<TypeIBaselineView> SeedBaselineAsync(bool includeAdditionalSource = false)
        {
            var aggregate = new ExaminationEngagement(TenantId, ProgramId, ExaminationId);
            Assert.Null(aggregate.Create(new ExaminationEngagementContent("type_i", "Audit LLP",
                new DateOnly(2026, 9, 30), null, ["security"], BoundaryId, []),
                UserMemberId, "Examiner", Now));
            var sources = new List<TypeIBaselineSourceReference>
            {
                new("approved_boundary", BoundaryId, BaselineSourceVersionId, 2,
                    new string('a', 64)),
            };
            if (includeAdditionalSource)
                sources.Add(new TypeIBaselineSourceReference("approved_control",
                    AdditionalSourceRecordId, AdditionalSourceVersionId, 1, new string('b', 64)));
            var preview = TypeIBaselinePreview.Create(TenantId, ProgramId, ExaminationId,
                sources);
            Assert.Null(aggregate.FreezeBaseline(1, BaselineId, preview,
                "The initial source manifest is complete.", UserMemberId, "Examiner", Now.AddMinutes(1)));
            await Writer.SaveAsync(aggregate, new RequestDispatchContext(RequestActor.System));
            return aggregate.Baseline!;
        }

        public async Task SeedDescriptionAsync()
        {
            var source = new SystemDescriptionSourceReference("approved_boundary", BoundaryId,
                BaselineSourceVersionId, 2, new string('a', 64), "current");
            var scope = new SystemDescriptionScope("type_i", ExaminationId, BaselineId, null, null);
            var content = new SystemDescriptionDraftContent("The Sample System", Sections(),
                [source], [], []);
            var aggregate = new DescriptionAggregate(TenantId, ProgramId, DescriptionId);
            Assert.Null(aggregate.Create(Uuid.CreateVersion4(), scope, content, UserMemberId,
                "author@example.test", Now.AddMinutes(2)));
            await Writer.SaveAsync(aggregate, new RequestDispatchContext(RequestActor.System));
        }

        public async Task<TypeIIPeriodView> SeedClosedTypeIIPeriodAsync()
        {
            var references = TypeIISourceReferences();
            var plan = new TypeIIPlanAggregate(TenantId, ProgramId, PlanId);
            var content = new TypeIIPlanContent(ExaminationId, Uuid.CreateVersion4(),
                new string('c', 64), Uuid.CreateVersion4(), new DateOnly(2026, 10, 1),
                new DateOnly(2027, 9, 30), "Audit LLP", ["security"], BaselineId,
                Uuid.CreateVersion4(), references,
                [new TypeIIControlPlanEntry(ControlId, ControlVersionId, "Quarterly review",
                    UserMemberId, "quarterly", 4, new DateOnly(2026, 10, 1), null)],
                [new TypeIICarriedForwardGap("risk", GapId, Uuid.CreateVersion4(), 1,
                    new string('d', 64), "carry")],
                [new TypeIIPlanGapAcknowledgement(GapId, "Known opening gap", "acknowledged",
                    "Owner and target date are recorded.")], []);
            Assert.Null(plan.Propose(content, UserMemberId, "Compliance lead", Now.AddMinutes(-3)));
            Assert.Null(plan.Review(plan.Revision, 1, "approve", "Plan reviewed.",
                RbacIds.Member(TenantId, ReviewerId), "Independent reviewer", Now.AddMinutes(-2)));
            Assert.Null(plan.Approve(plan.Revision, 1, "Approved period plan.",
                RbacIds.Member(TenantId, ApproverId), "CISO", "Chief Information Security Officer",
                Now.AddMinutes(-1)));
            await Writer.SaveAsync(plan, new RequestDispatchContext(RequestActor.System));

            var period = new TypeIIPeriodAggregate(TenantId, ProgramId, PeriodId);
            Assert.Null(period.Start(plan.Current!, new TypeIIPeriodOpeningContent(ExaminationId,
                Uuid.CreateVersion4(), new string('e', 64), references, [GapId]),
                UserMemberId, "Compliance lead", Now));
            var occurrences = period.Current!.ExpectedOccurrences.Select(static item => item.OccurrenceId)
                .OrderBy(static id => id.ToString(), StringComparer.Ordinal).ToArray();
            Assert.Null(period.RecordCloseValidation(period.Revision,
                new TypeIIPeriodCloseValidationContent(references, occurrences, occurrences, [], [],
                    [GapId]), RbacIds.Member(TenantId, ValidatorId), "Close validator",
                Now.AddDays(400)));
            Assert.Null(period.ApproveClose(period.Revision, [GapId], "Reviewed close.",
                RbacIds.Member(TenantId, ApproverId), "CEO", "Chief Executive",
                Now.AddDays(400).AddHours(1)));
            Assert.Null(period.FreezeClose(period.Revision, new TypeIIPeriodCloseSnapshotContent(
                CloseSnapshotId, references, occurrences, [GapId]),
                RbacIds.Member(TenantId, ApproverId), "Chief Executive", Now.AddDays(400).AddHours(2)));
            await Writer.SaveAsync(period, new RequestDispatchContext(RequestActor.System));
            return period.Current!;
        }

        public async Task SeedTypeIIDescriptionAsync(Uuid closeSnapshotId)
        {
            var scope = new SystemDescriptionScope("type_ii", ExaminationId, BaselineId,
                PeriodId, closeSnapshotId);
            var content = new SystemDescriptionDraftContent("The Sample System", Sections(), [], [], []);
            var aggregate = new DescriptionAggregate(TenantId, ProgramId, DescriptionId);
            Assert.Null(aggregate.Create(Uuid.CreateVersion4(), scope, content, UserMemberId,
                "author@example.test", Now.AddMinutes(2)));
            await Writer.SaveAsync(aggregate, new RequestDispatchContext(RequestActor.System));
        }

        IReadOnlyList<ExaminationContentReference> TypeIISourceReferences() =>
        [
            new("boundary", BoundaryId, BaselineSourceVersionId, 1, new string('a', 64)),
            new("control", AdditionalSourceRecordId, AdditionalSourceVersionId, 2,
                new string('b', 64)),
        ];

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

}
