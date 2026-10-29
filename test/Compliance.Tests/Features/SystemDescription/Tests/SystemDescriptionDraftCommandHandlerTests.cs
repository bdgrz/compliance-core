using Cntryl.Fitz.Testing;
using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Tests.Testing;
using DescriptionAggregate = Bdgrz.Compliance.Features.SystemDescription.Rules.SystemDescriptionRules;
using TypeIIPlanAggregate = Bdgrz.Compliance.Features.Examinations.Aggregates.TypeIIPlan;
using TypeIIPeriodAggregate = Bdgrz.Compliance.Features.Operations.Rules.TypeIIPeriodRules;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Services;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Responsibilities.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Readers;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Handlers;
using Bdgrz.Compliance.Features.SystemDescription.Models;
using Bdgrz.Compliance.Features.SystemDescription.Readers;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.Work.Authorizers;
namespace Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;

public sealed partial class SystemDescriptionDraftCommandHandlerTests
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
        var saved = await fixture.Descriptions.ReadAsync(fixture.TenantId,
            fixture.ProgramId, fixture.DescriptionId);

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
        var saved = await fixture.Descriptions.ReadAsync(fixture.TenantId,
            fixture.ProgramId, fixture.DescriptionId);

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
        var saved = await fixture.Descriptions.ReadAsync(fixture.TenantId,
            fixture.ProgramId, fixture.DescriptionId);

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
        var request = new ReviseSystemDescriptionDraft(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            fixture.DescriptionId, 1, "Updated System", sections, "Clarified service scope.");

        // Act
        var result = await fixture.ReviseHandler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Descriptions.ReadAsync(fixture.TenantId,
            fixture.ProgramId, fixture.DescriptionId);

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
        var request = new ReviseSystemDescriptionDraft(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            fixture.DescriptionId, 0, "Stale System", Sections(), "Stale edit.");

        // Act
        var result = await fixture.ReviseHandler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Descriptions.ReadAsync(fixture.TenantId,
            fixture.ProgramId, fixture.DescriptionId);

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
        var request = new BindSystemDescriptionSources(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            fixture.DescriptionId, 1, [fixture.BaselineSourceVersionId]);

        // Act
        var result = await fixture.BindSourcesHandler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Descriptions.ReadAsync(fixture.TenantId,
            fixture.ProgramId, fixture.DescriptionId);

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
        var request = new BindSystemDescriptionSources(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            fixture.DescriptionId, 1, [Uuid.CreateVersion4()]);

        // Act
        var result = await fixture.BindSourcesHandler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Descriptions.ReadAsync(fixture.TenantId,
            fixture.ProgramId, fixture.DescriptionId);

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
        var request = new BindSystemDescriptionSources(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            fixture.DescriptionId, 1, [fixture.BaselineSourceVersionId]);

        // Act
        var result = await fixture.BindSourcesHandler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Descriptions.ReadAsync(fixture.TenantId,
            fixture.ProgramId, fixture.DescriptionId);

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
        var request = new BindSystemDescriptionSources(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            fixture.DescriptionId, 1, sourceVersionIds);

        // Act
        var result = await fixture.BindSourcesHandler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Descriptions.ReadAsync(fixture.TenantId,
            fixture.ProgramId, fixture.DescriptionId);

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
        var request = new BindSystemDescriptionSources(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            fixture.DescriptionId, 1, period.CloseSnapshots[^1].SourceReferences
                .Select(static source => source.VersionId).ToArray());

        // Act
        var result = await fixture.BindSourcesHandler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Descriptions.ReadAsync(fixture.TenantId,
            fixture.ProgramId, fixture.DescriptionId);

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
        public InMemoryEventStore PeriodEvents { get; } = new();
        public IImmutableContentStore PeriodContent { get; } = TypeIIPeriodSeeds.NewContentStore();
        public SystemDescriptionReader Descriptions => new(Reader, PeriodEvents, PeriodContent);
        public SystemDescriptionOperations Operations => new(new NativeOperationExecutor(Reader, Executor, PeriodContent), Descriptions, PeriodContent, _clock);
        public SystemDescriptionSourceResolver Sources => new(Reader, Periods, new SystemDescriptionSnapshotSources(PeriodEvents, PeriodContent), Examinations);
        public TypeIIPeriodReader Periods => new(Reader, PeriodEvents, PeriodContent);
        public ExaminationEngagementReader Examinations => new(Reader, PeriodEvents, PeriodContent);
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }
        public IAggregateExecutor Executor { get; }

        ClaimsPrincipal Actor => new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", UserId.ToString()),
                new Claim("email", "author@example.test")], "BdgrzSession"));

        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(PeriodEvents);
            services.AddPortia();
            ExaminationSeeds.Configure(services, PeriodEvents);
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        }

        public CreateSystemDescriptionDraftHandler CreateHandler() => new(Operations, Sources);

        public ReviseSystemDescriptionDraftHandler ReviseHandler() => new(Operations);

        public BindSystemDescriptionSourcesHandler BindSourcesHandler() => new(Operations, Sources);

        public Cntryl.Portia.RequestContext<T> Context<T>(T request) where T : IRequestBase => new(request,
            new RequestDispatchContext(Actor, new HttpInvocation("POST", "/system-descriptions",
                "/system-descriptions", "system-description-draft-test")));

        public static Cntryl.Portia.RequestContext<T> SystemContext<T>(T request) where T : IRequestBase =>
            new(request, new RequestDispatchContext(RequestActor.System,
                new HttpInvocation("POST", "/system-descriptions", "/system-descriptions",
                    "system-description-draft-test")));

        public async Task<TypeIBaselineView> SeedBaselineAsync(bool includeAdditionalSource = false)
        {
            var aggregate = new ExaminationEngagementRules(TenantId, ProgramId, ExaminationId);
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
            var preview = TypeIBaselinePreviewTestFactory.Create(TenantId, ProgramId, ExaminationId,
                sources);
            Assert.Null(aggregate.FreezeBaseline(1, BaselineId, preview,
                "The initial source manifest is complete.", UserMemberId, "Examiner", Now.AddMinutes(1)));
            await ExaminationSeeds.WriteAsync(Reader, Writer, PeriodEvents, PeriodContent, aggregate);
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
            await DescriptionSeeds.WriteAsync(Writer, PeriodContent, aggregate);
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
                    UserMemberId, "quarterly", 4, new DateOnly(2026, 10, 1), null,
                    ExpectedEvidenceDescriptions: ["Dated review record"])],
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
            await TypeIIPeriodSeeds.WriteAsync(Writer, PeriodContent, period);
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
            await DescriptionSeeds.WriteAsync(Writer, PeriodContent, aggregate);
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
