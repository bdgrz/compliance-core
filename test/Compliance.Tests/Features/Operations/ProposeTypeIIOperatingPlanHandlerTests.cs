using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Features.SystemDescription;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using AuditOutcomeAggregate = Bdgrz.Compliance.Features.Examinations.AuditOutcome;
using ExaminationEngagementAggregate = Bdgrz.Compliance.Features.Examinations.ExaminationEngagement;
using SystemDescriptionAggregate = Bdgrz.Compliance.Features.SystemDescription.SystemDescription;
using TypeIIPlanAggregate = Bdgrz.Compliance.Features.Examinations.TypeIIPlan;

namespace Bdgrz.Compliance.Tests.Features.Operations;

public sealed class ProposeTypeIIOperatingPlanHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
    static readonly string[] RequiredSections =
    [
        "services", "commitments_requirements", "components_infrastructure_software", "people",
        "procedures", "information_data_flows", "system_boundary", "control_environment",
        "user_entity_controls", "subservice_organizations", "exclusions", "relevant_changes",
    ];

    [Fact]
    public async Task ShouldProposeSourceBoundPlanGivenPersonalActor()
    {
        // Arrange
        await using var fixture = new Fixture();
        var content = await fixture.SeedTypeISourceChainAsync();
        var request = fixture.Request(content);

        // Act
        var result = await fixture.Handler.HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TypeIIPlanAggregate(fixture.TenantId,
            fixture.ProgramId, fixture.PlanId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("proposed", saved.Current!.Status);
        var proposed = saved.Current.Current!;
        Assert.Equal(content.ExaminationId, proposed.Content.ExaminationId);
        Assert.Equal(content.SourceSnapshotId, proposed.Content.SourceSnapshotId);
        Assert.Equal(content.SourceSnapshotSha256, proposed.Content.SourceSnapshotSha256);
        Assert.Equal(content.TypeIOutcomeId, proposed.Content.TypeIOutcomeId);
        Assert.Equal(content.CarryForwardInputs.OrderBy(item => item.SourceType, StringComparer.Ordinal),
            proposed.Content.CarryForwardInputs);
        var expectedControl = Assert.Single(content.Controls.OrderBy(item =>
            item.ControlId.ToString(), StringComparer.Ordinal));
        var savedControl = Assert.Single(proposed.Content.Controls);
        Assert.Equal(expectedControl with
        {
            ExpectedEvidenceDescriptions = savedControl.ExpectedEvidenceDescriptions,
        }, savedControl);
        Assert.Equal(expectedControl.ExpectedEvidenceDescriptions,
            savedControl.ExpectedEvidenceDescriptions);
        Assert.Equal(content.CarriedForwardGaps.OrderBy(item => item.SourceKind, StringComparer.Ordinal),
            proposed.Content.CarriedForwardGaps);
        Assert.Equal(content.AcknowledgedGaps.OrderBy(item => item.GapId.ToString(), StringComparer.Ordinal),
            proposed.Content.AcknowledgedGaps);
        Assert.Equal(content.RequiredChanges.OrderBy(item => item.ChangeId.ToString(), StringComparer.Ordinal),
            proposed.Content.RequiredChanges);
        Assert.Equal(fixture.ActorMemberId, proposed.ProposedByMemberId);
    }

    [Fact]
    public async Task ShouldRejectDifferentBaselineGivenTypeIIOperatingPlanProposal()
    {
        // Arrange
        await using var fixture = new Fixture();
        var content = await fixture.SeedTypeISourceChainAsync();
        var request = fixture.Request(content with { SourceSnapshotId = Uuid.CreateVersion4() });

        // Act
        var result = await fixture.Handler.HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TypeIIPlanAggregate(fixture.TenantId,
            fixture.ProgramId, fixture.PlanId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(0, saved.Revision);
    }

    [Fact]
    public async Task ShouldRejectCadenceDifferentFromApprovedControlPlanGivenProposal()
    {
        // Arrange
        await using var fixture = new Fixture();
        var content = await fixture.SeedTypeISourceChainAsync();
        var request = fixture.Request(content with
        {
            Controls = [content.Controls[0] with { Cadence = "monthly" }],
        });

        // Act
        var result = await fixture.Handler.HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TypeIIPlanAggregate(fixture.TenantId,
            fixture.ProgramId, fixture.PlanId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(0, saved.Revision);
    }

    [Fact]
    public async Task ShouldRejectDueWindowDifferentFromApprovedControlPlanGivenProposal()
    {
        // Arrange
        await using var fixture = new Fixture();
        var content = await fixture.SeedTypeISourceChainAsync();
        var request = fixture.Request(content with
        {
            Controls = [content.Controls[0] with { DueWithinDays = 6 }],
        });

        // Act
        var result = await fixture.Handler.HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TypeIIPlanAggregate(fixture.TenantId,
            fixture.ProgramId, fixture.PlanId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(0, saved.Revision);
    }

    [Fact]
    public async Task ShouldRejectExpectedEvidenceDifferentFromApprovedControlVersionGivenProposal()
    {
        // Arrange
        await using var fixture = new Fixture();
        var content = await fixture.SeedTypeISourceChainAsync();
        var request = fixture.Request(content with
        {
            Controls = [content.Controls[0] with
            {
                ExpectedEvidenceDescriptions = ["Substituted evidence description"],
            }],
        });

        // Act
        var result = await fixture.Handler.HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TypeIIPlanAggregate(fixture.TenantId,
            fixture.ProgramId, fixture.PlanId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(0, saved.Revision);
    }

    [Fact]
    public async Task ShouldRejectOmittedBaselineSourceGivenTypeIIOperatingPlanProposal()
    {
        // Arrange
        await using var fixture = new Fixture();
        var content = await fixture.SeedTypeISourceChainAsync();
        var request = fixture.Request(content with
        {
            CarryForwardInputs = content.CarryForwardInputs.Skip(1).ToArray(),
        });

        // Act
        var result = await fixture.Handler.HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TypeIIPlanAggregate(fixture.TenantId,
            fixture.ProgramId, fixture.PlanId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(0, saved.Revision);
    }

    [Fact]
    public async Task ShouldRejectOmittedOpenFindingGivenTypeIIOperatingPlanProposal()
    {
        // Arrange
        await using var fixture = new Fixture();
        var content = await fixture.SeedTypeISourceChainAsync();
        var request = fixture.Request(content with
        {
            CarriedForwardGaps = [],
            AcknowledgedGaps = [],
        });

        // Act
        var result = await fixture.Handler.HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TypeIIPlanAggregate(fixture.TenantId,
            fixture.ProgramId, fixture.PlanId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(0, saved.Revision);
    }

    [Fact]
    public async Task ShouldRejectChangedFindingRevisionGivenTypeIIOperatingPlanProposal()
    {
        // Arrange
        await using var fixture = new Fixture();
        var content = await fixture.SeedTypeISourceChainAsync();
        await fixture.ReviseFindingAsync();
        var request = fixture.Request(content);

        // Act
        var result = await fixture.Handler.HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TypeIIPlanAggregate(fixture.TenantId,
            fixture.ProgramId, fixture.PlanId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(0, saved.Revision);
    }

    [Fact]
    public async Task ShouldRejectUnattributedFindingOwnerGivenTypeIIOperatingPlanProposal()
    {
        // Arrange
        await using var fixture = new Fixture();
        var content = await fixture.SeedTypeISourceChainAsync();
        var request = fixture.Request(content with
        {
            CarriedForwardGaps = [content.CarriedForwardGaps[0] with
            {
                OwnerMemberId = Uuid.CreateVersion4(),
            }],
        });

        // Act
        var result = await fixture.Handler.HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TypeIIPlanAggregate(fixture.TenantId,
            fixture.ProgramId, fixture.PlanId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(0, saved.Revision);
    }

    [Fact]
    public async Task ShouldRejectAcceptedDispositionWithoutActiveFindingAcceptanceGivenProposal()
    {
        // Arrange
        await using var fixture = new Fixture();
        var content = await fixture.SeedTypeISourceChainAsync();
        var request = fixture.Request(content with
        {
            CarriedForwardGaps = [content.CarriedForwardGaps[0] with
            {
                Disposition = "accepted",
            }],
        });

        // Act
        var result = await fixture.Handler.HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TypeIIPlanAggregate(fixture.TenantId,
            fixture.ProgramId, fixture.PlanId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(0, saved.Revision);
    }

    [Fact]
    public async Task ShouldRejectDuplicateFindingGivenTypeIIOperatingPlanProposal()
    {
        // Arrange
        await using var fixture = new Fixture();
        var content = await fixture.SeedTypeISourceChainAsync();
        await fixture.SeedAdditionalFindingAsync();
        var duplicate = content.CarriedForwardGaps[0] with
        {
            SourceVersionId = Uuid.CreateVersion4(),
        };
        var request = fixture.Request(content with
        {
            CarriedForwardGaps = [content.CarriedForwardGaps[0], duplicate],
            AcknowledgedGaps = [content.AcknowledgedGaps[0],
                content.AcknowledgedGaps[0] with
                {
                    GapId = fixture.SecondFindingId,
                    Summary = "Second current finding",
                }],
        });

        // Act
        var result = await fixture.Handler.HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TypeIIPlanAggregate(fixture.TenantId,
            fixture.ProgramId, fixture.PlanId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(0, saved.Revision);
    }

    [Fact]
    public async Task ShouldCaptureCurrentFindingRevisionAfterBaselineFreezeGivenProposal()
    {
        // Arrange
        await using var fixture = new Fixture();
        var content = await fixture.SeedTypeISourceChainAsync();
        var currentFinding = await fixture.ReviseFindingAsync();
        var currentGap = content.CarriedForwardGaps[0] with
        {
            SourceVersionId = currentFinding.VersionId,
            SourceVersion = currentFinding.Revision,
            SourceSha256 = currentFinding.ContentSha256,
        };
        var request = fixture.Request(content with { CarriedForwardGaps = [currentGap] });

        // Act
        var result = await fixture.Handler.HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TypeIIPlanAggregate(fixture.TenantId,
            fixture.ProgramId, fixture.PlanId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(currentFinding.VersionId, saved.Current!.Current!.Content.CarriedForwardGaps[0]
            .SourceVersionId);
        Assert.Equal(fixture.MemberId, saved.Current.Current.Content.CarriedForwardGaps[0]
            .OwnerMemberId);
    }

    [Fact]
    public async Task ShouldRejectSystemActorGivenTypeIIOperatingPlanProposal()
    {
        // Arrange
        await using var fixture = new Fixture();
        var request = fixture.Request(await fixture.SeedTypeISourceChainAsync());

        // Act
        var result = await fixture.Handler.HandleAsync(fixture.Context(request,
            RequestActor.System), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TypeIIPlanAggregate(fixture.TenantId,
            fixture.ProgramId, fixture.PlanId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(0, saved.Revision);
    }

    [Fact]
    public async Task ShouldPreserveOriginalProposalTimeGivenSameCommandRetriedLater()
    {
        // Arrange
        await using var fixture = new Fixture();
        var request = fixture.Request(await fixture.SeedTypeISourceChainAsync());
        var context = fixture.Context(request);
        Assert.True((await fixture.Handler.HandleAsync(context, CancellationToken.None)).IsSuccess);
        fixture.Clock.Set(Now.AddDays(1));

        // Act
        var retry = await fixture.Handler.HandleAsync(context, CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TypeIIPlanAggregate(fixture.TenantId,
            fixture.ProgramId, fixture.PlanId));

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(1, saved.Revision);
        Assert.Single(saved.Current!.Versions);
        Assert.Equal(Now, saved.Current.Current!.ProposedAt);
    }

    [Fact]
    public async Task ShouldTreatSameIntentAsIdempotentGivenOwnerSuspendedAfterProposal()
    {
        // Arrange
        await using var fixture = new Fixture();
        var request = fixture.Request(await fixture.SeedTypeISourceChainAsync());
        var context = fixture.Context(request);
        Assert.True((await fixture.Handler.HandleAsync(context, CancellationToken.None)).IsSuccess);
        await fixture.SuspendOwnerAsync();
        fixture.Clock.Set(Now.AddHours(1));

        // Act
        var retry = await fixture.Handler.HandleAsync(context, CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TypeIIPlanAggregate(fixture.TenantId,
            fixture.ProgramId, fixture.PlanId));

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(1, saved.Revision);
        Assert.Single(saved.Current!.Versions);
        Assert.Equal(Now, saved.Current.Current!.ProposedAt);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider provider;
        readonly AsyncServiceScope scope;
        readonly ClaimsPrincipal actor;

        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid ExaminationId { get; } = Uuid.CreateVersion4();
        public Uuid PlanId { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public Uuid OwnerUserId { get; } = Uuid.CreateVersion4();
        public Uuid ActorMemberId => RbacIds.Member(TenantId, UserId);
        public Uuid MemberId => RbacIds.Member(TenantId, OwnerUserId);
        public Uuid AuthorId { get; } = Uuid.CreateVersion4();
        public Uuid ReviewerId { get; } = Uuid.CreateVersion4();
        public Uuid ExecutiveId { get; } = Uuid.CreateVersion4();
        public Uuid OutcomeReviewerId { get; } = Uuid.CreateVersion4();
        public Uuid BoundaryId { get; } = Uuid.CreateVersion4();
        public Uuid ControlId { get; } = Uuid.CreateVersion4();
        public Uuid ControlVersionId => ControlVersionIds.Initial(ControlId);
        public Uuid PolicyId { get; } = Uuid.CreateVersion4();
        public Uuid RiskId { get; } = Uuid.CreateVersion4();
        public Uuid VendorId { get; } = Uuid.CreateVersion4();
        public Uuid FindingId { get; } = Uuid.CreateVersion4();
        public Uuid SecondFindingId { get; } = Uuid.CreateVersion4();
        public Uuid ChangeId { get; } = Uuid.CreateVersion4();
        public Uuid BaselineId { get; } = Uuid.CreateVersion4();
        public Uuid DescriptionId { get; } = Uuid.CreateVersion4();
        public Uuid DescriptionVersionId { get; } = Uuid.CreateVersion4();
        public Uuid OutcomeId { get; } = Uuid.CreateVersion4();
        public DateOnly PeriodStart { get; } = new(2026, 11, 1);
        public DateOnly PeriodEnd { get; } = new(2027, 10, 31);
        public MutableTimeProvider Clock { get; } = new(Now);
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }
        public IAggregateExecutor Executor { get; }
        public ProposeTypeIIOperatingPlanHandler Handler { get; }

        public Fixture()
        {
            actor = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("iss", "bdgrz"), new Claim("sub", UserId.ToString()),
                    new Claim("name", "Plan author")], "BdgrzSession"));
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddPortia();
            provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            scope = provider.CreateAsyncScope();
            Reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Executor = scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
            Handler = new ProposeTypeIIOperatingPlanHandler(Executor, Reader, Clock);
        }

        public ProposeTypeIIOperatingPlan Request(TypeIIPlanContent content) =>
            new(TenantId, ProgramId, PlanId, ExaminationId, content);

        public Cntryl.Portia.RequestContext<T> Context<T>(T request,
            ClaimsPrincipal? principal = null) where T : IRequestBase =>
            new(request, new RequestDispatchContext(principal ?? actor, new HttpInvocation("POST",
                "/api/v1/tenants/tenant/programs/program/examinations/exam/type-ii-plans/plan/proposals",
                "/api/v1/type-ii-plans/proposals", "propose-type-ii-plan-test")));

        public async Task<TypeIIPlanContent> SeedTypeISourceChainAsync()
        {
            foreach (var userId in new[] { UserId, OwnerUserId })
            {
                var member = new Member(TenantId, userId);
                Assert.True(member.Register().IsSuccess);
                await Writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));
            }

            var findings = new RemediationLedger(TenantId, ProgramId);
            Assert.Null(findings.Raise(FindingId,
                new FindingSource("readiness_gap", Uuid.CreateVersion4(), "1", "Known opening gap."),
                "Known opening finding", "A readiness gap remains open.", "medium",
                "System access", MemberId, new DateOnly(2026, 12, 1), [],
                ActorReference.ForMember(ActorMemberId, "Plan author"), Now.AddMinutes(-21)));
            await Writer.SaveAsync(findings, new RequestDispatchContext(RequestActor.System));
            var finding = findings.Read(FindingId, Now)!;
            var findingIdentity = FindingContentIdentityFactory.Create(finding,
                findings.ReadClosures(FindingId));

            var control = new ControlDraft(TenantId, ControlId);
            Assert.True(control.Create(ProgramId, Uuid.CreateVersion4(), "AC-T1-01",
                new ControlDraftContent("Quarterly access review", "Review user access",
                    "Management reviews access quarterly.",
                    "The security lead reviews the access roster.", ["Dated review record"]),
                AuthorId, "Control author", Now.AddMinutes(-20)).IsSuccess);
            var controlScope = new ResponsibilityScope("control", ControlId, ControlVersionId, 1);
            Assert.Null(control.AssignResponsibility(controlScope, Uuid.CreateVersion4(), MemberId,
                ResponsibilityType.ControlOwner, AuthorId, "Control author", Now.AddMinutes(-18),
                Now.AddMinutes(-19), null, []));
            var controlReviewId = Uuid.CreateVersion4();
            Assert.Null(control.Review(ProgramId, 1, controlReviewId, "accept",
                "The quarterly evidence is defined.", ReviewerId, "Control reviewer",
                Now.AddMinutes(-17)));
            Assert.Null(control.Approve(ProgramId, 1, Uuid.CreateVersion4(), controlReviewId,
                new DateOnly(2026, 10, 1), "Approved for operation.", new HashSet<Uuid> { MemberId },
                ExecutiveId, "Chief Executive", Now.AddMinutes(-16)));
            var controlVersion = control.ApprovedVersion!;
            await Writer.SaveAsync(control, new RequestDispatchContext(RequestActor.System));

            var controlOperations = new ControlOperationsLedger(TenantId, ProgramId);
            var operatingPlanId = Uuid.CreateVersion4();
            Assert.Null(controlOperations.ProposePlan(ControlId, 0, operatingPlanId, controlVersion,
                new OperatingHolder("member", MemberId), null, ReviewerId, false,
                new ControlCadence(ControlCadenceSchedule.Recurring, "quarterly", PeriodStart, 5),
                PeriodStart, "Operate quarterly and retain dated evidence.", AuthorId,
                "Control plan author", Now.AddMinutes(-14), null));
            Assert.Null(controlOperations.ApprovePlan(ControlId, 1, operatingPlanId,
                "The quarterly schedule and owner are approved.", ExecutiveId, "Chief Executive",
                Now.AddMinutes(-13), null));
            await Writer.SaveAsync(controlOperations,
                new RequestDispatchContext(RequestActor.System));

            var boundaryVersionId = Uuid.CreateVersion4();
            var boundaryHash = Hash('a');
            var description = new SystemDescriptionAggregate(TenantId, ProgramId, DescriptionId);
            var descriptionContent = new SystemDescriptionDraftContent("Sample system", Sections(),
                [new SystemDescriptionSourceReference("approved_boundary", BoundaryId,
                    boundaryVersionId, 1, boundaryHash, "current")], [], []);
            Assert.Null(description.Create(DescriptionVersionId,
                new SystemDescriptionScope("type_i", ExaminationId, BaselineId, null, null),
                descriptionContent, AuthorId, "Description author", Now.AddMinutes(-10)));
            Assert.Null(description.Review(1, ReviewerId, "Independent reviewer", "approve",
                "The system description is complete.", Now.AddMinutes(-9)));
            Assert.Null(description.Approve(1, ExecutiveId, "Chief Executive",
                "Approved the Type I system description.", Now.AddMinutes(-8)));
            var descriptionVersion = description.FindVersion(DescriptionVersionId)!;

            var sources = new[]
            {
                new TypeIBaselineSourceReference("approved_boundary", BoundaryId,
                    boundaryVersionId, 1, boundaryHash),
                new TypeIBaselineSourceReference("approved_control", ControlId,
                    ControlVersionId, controlVersion.Revision, Hash('b')),
                new TypeIBaselineSourceReference("policy", PolicyId, Uuid.CreateVersion4(),
                    1, Hash('c')),
                new TypeIBaselineSourceReference("risk", RiskId, Uuid.CreateVersion4(),
                    1, Hash('d')),
                new TypeIBaselineSourceReference("vendor", VendorId, Uuid.CreateVersion4(),
                    1, Hash('e')),
                new TypeIBaselineSourceReference("finding", FindingId, findingIdentity.VersionId,
                    findingIdentity.Revision, findingIdentity.ContentSha256),
                new TypeIBaselineSourceReference("change", ChangeId, Uuid.CreateVersion4(),
                    1, Hash('1')),
                new TypeIBaselineSourceReference("system_description", DescriptionId,
                    DescriptionVersionId, 1, descriptionVersion.ContentSha256),
            };
            var engagement = new ExaminationEngagementAggregate(TenantId, ProgramId, ExaminationId);
            Assert.Null(engagement.Create(new ExaminationEngagementContent("type_i", "Audit Firm",
                new DateOnly(2026, 9, 30), null, ["security"], BoundaryId, []),
                AuthorId, "Examiner", Now.AddMinutes(-12)));
            var preview = TypeIBaselinePreview.Create(TenantId, ProgramId, ExaminationId, sources);
            Assert.Null(engagement.FreezeBaseline(1, BaselineId, preview,
                "The frozen baseline includes approved controls and system description.", AuthorId,
                "Examiner", Now.AddMinutes(-7)));
            Assert.Null(description.BindToExamination(1, ExaminationId, "type_i_baseline",
                BaselineId, ExecutiveId, "Chief Executive", Now.AddMinutes(-6)));
            await Writer.SaveAsync(engagement, new RequestDispatchContext(RequestActor.System));
            await Writer.SaveAsync(description, new RequestDispatchContext(RequestActor.System));

            var outcome = new AuditOutcomeAggregate(TenantId, ProgramId, OutcomeId);
            Assert.Null(outcome.Record(new AuditOutcomeContent(ExaminationId, "type_i", BaselineId,
                engagement.Baseline!.ContentSha256, Uuid.CreateVersion4(), 1, Uuid.CreateVersion4(),
                Hash('2'), "auditor", "unmodified_opinion", "No material exceptions.",
                "Report reference", null, null, new DateOnly(2026, 10, 1)), OutcomeReviewerId,
                "Auditor", Now.AddMinutes(-5)));
            await Writer.SaveAsync(outcome, new RequestDispatchContext(RequestActor.System));

            var references = sources.Select(ToReference).ToArray();
            var gap = new TypeIICarriedForwardGap("finding", FindingId,
                findingIdentity.VersionId, findingIdentity.Revision,
                findingIdentity.ContentSha256, "carry", MemberId);
            return new TypeIIPlanContent(ExaminationId, BaselineId,
                engagement.Baseline.ContentSha256, DescriptionVersionId, PeriodStart, PeriodEnd,
                "Audit Firm", ["security"], BaselineId, OutcomeId, references,
                [new TypeIIControlPlanEntry(ControlId, ControlVersionId, controlVersion.Content.Title,
                    MemberId, "quarterly", 4, PeriodStart, null, 5,
                    controlVersion.Content.ExpectedEvidenceDescriptions)], [gap],
                [new TypeIIPlanGapAcknowledgement(FindingId, "Known opening finding",
                    "acknowledged", "Owner and due date were reviewed.")],
                [new TypeIIPlannedChange(ChangeId, "Complete the required control change.",
                    new DateOnly(2026, 12, 1))]);
        }

        public async Task SuspendOwnerAsync()
        {
            var owner = await Reader.HydrateAsync(new Member(TenantId, OwnerUserId));
            Assert.True(owner.Suspend(AuthorId, "Administrator", Now.AddMinutes(1),
                "The control owner left the organization.").IsSuccess);
            await Writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System));
        }

        public async Task<FindingContentIdentity> ReviseFindingAsync()
        {
            var findings = await Reader.HydrateAsync(new RemediationLedger(TenantId, ProgramId));
            var current = findings.Read(FindingId, Now)!;
            Assert.Null(findings.Revise(FindingId, current.Revision, "high", MemberId,
                current.DueOn, current.AffectedScope, "Updated current finding state.",
                "Refresh finding for the Type II opening plan.",
                ActorReference.ForMember(ActorMemberId, "Plan author"), Now.AddMinutes(1)));
            await Writer.SaveAsync(findings, new RequestDispatchContext(RequestActor.System));
            var revised = findings.Read(FindingId, Now.AddMinutes(1))!;
            return FindingContentIdentityFactory.Create(revised,
                findings.ReadClosures(FindingId));
        }

        public async Task SeedAdditionalFindingAsync()
        {
            var findings = await Reader.HydrateAsync(new RemediationLedger(TenantId, ProgramId));
            Assert.Null(findings.Raise(SecondFindingId,
                new FindingSource("readiness_gap", Uuid.CreateVersion4(), "2",
                    "Another known opening gap."),
                "Second opening finding", "A second readiness gap remains open.", "low",
                "Data retention", MemberId, new DateOnly(2026, 12, 15), [],
                ActorReference.ForMember(ActorMemberId, "Plan author"), Now.AddMinutes(-20)));
            await Writer.SaveAsync(findings, new RequestDispatchContext(RequestActor.System));
        }

        static ExaminationContentReference ToReference(TypeIBaselineSourceReference source) =>
            new(source.Kind, source.RecordId, source.VersionId, source.Revision, source.ContentSha256);

        static SystemDescriptionSection[] Sections() => RequiredSections.Select(id =>
            new SystemDescriptionSection(id, id.Replace('_', ' '), "Current system fact.",
                "complete", null)).ToArray();

        static string Hash(char value) => new(value, 64);

        public async ValueTask DisposeAsync()
        {
            await scope.DisposeAsync();
            await provider.DisposeAsync();
        }
    }

    sealed class MutableTimeProvider(DateTimeOffset current) : TimeProvider
    {
        DateTimeOffset current = current;
        public override DateTimeOffset GetUtcNow() => current;
        public void Set(DateTimeOffset value) => current = value;
    }
}
