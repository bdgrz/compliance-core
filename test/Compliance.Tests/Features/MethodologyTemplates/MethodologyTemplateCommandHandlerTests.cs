using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Fitz.Testing;
using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Content;
using System.Security.Claims;
using Bdgrz.Compliance.Features.MethodologyTemplates;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.MethodologyTemplates;

public sealed class MethodologyTemplateCommandHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 17, 15, 0, 0, TimeSpan.Zero);
    static readonly Uuid TemplateId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldRejectContributionSelfReviewGivenSameUserAcrossTenantAndPlatformContexts()
    {
        // Arrange
        await using var scenario = new Scenario();
        var tenant = Uuid.CreateVersion4();
        var subject = Uuid.CreateVersion4();
        var contributionId = Uuid.CreateVersion4();
        var propose = new ProposeDeidentifiedTemplateContribution(tenant, contributionId, Uuid.CreateVersion4(),
            1, new MethodologyTemplateRecordContent("review-access", "control", "Review access",
                "Review access quarterly", []), "Removed client identifiers");
        var created = await new ProposeDeidentifiedTemplateContributionHandler(scenario.ContributionOperations())
            .HandleAsync(Scenario.Context(propose, subject), CancellationToken.None);
        Assert.True(created.IsSuccess, created.Error?.Message);
        Assert.NotEqual(RbacIds.Member(tenant, subject), RbacIds.Member(Uuid.Empty, subject));

        // Act
        var reviewed = await new ReviewTemplateContributionHandler(scenario.ContributionOperations())
            .HandleAsync(Scenario.Context(new ReviewTemplateContribution(Uuid.CreateVersion4(), tenant,
                contributionId, "approved", "My own proposal"), subject), CancellationToken.None);
        var owner = await scenario.Reader.HydrateAsync(new MethodologyTemplateContribution(tenant, contributionId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, reviewed.Error?.Kind);
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public async Task ShouldReplayOriginalTemplateCommandsGivenLaterPublicationAndClock()
    {
        // Arrange
        await using var scenario = new Scenario();
        var author = Uuid.CreateVersion4();
        var reviewer = Uuid.CreateVersion4();
        var approver = Uuid.CreateVersion4();
        var create = new CreateMethodologyTemplate(TemplateId, Content("Original procedure"), "Initial");
        var revise = new ReviseMethodologyTemplateDraft(Uuid.CreateVersion4(), TemplateId, 1,
            Content("Revised procedure"), "Revise");
        var review = new ReviewMethodologyTemplate(Uuid.CreateVersion4(), TemplateId, 2, "approved", "Reviewed");
        var approve = new ApproveMethodologyTemplateVersion(Uuid.CreateVersion4(), TemplateId, 2, "Publish");
        var retire = new RetireMethodologyTemplate(Uuid.CreateVersion4(), TemplateId, 4,
            new DateOnly(2026, 10, 31), "Retire");
        Assert.True((await new CreateMethodologyTemplateHandler(scenario.Executor, scenario.Clock)
            .HandleAsync(Scenario.Context(create, author), CancellationToken.None)).IsSuccess);
        Assert.True((await new ReviseMethodologyTemplateDraftHandler(scenario.Executor, scenario.Clock)
            .HandleAsync(Scenario.Context(revise, author), CancellationToken.None)).IsSuccess);
        Assert.True((await new ReviewMethodologyTemplateHandler(scenario.Executor, scenario.Clock)
            .HandleAsync(Scenario.Context(review, reviewer), CancellationToken.None)).IsSuccess);
        Assert.True((await new ApproveMethodologyTemplateVersionHandler(scenario.Executor, scenario.Clock)
            .HandleAsync(Scenario.Context(approve, approver), CancellationToken.None)).IsSuccess);
        Assert.True((await new RetireMethodologyTemplateHandler(scenario.Executor, scenario.Clock)
            .HandleAsync(Scenario.Context(retire, approver), CancellationToken.None)).IsSuccess);
        var before = await scenario.Reader.HydrateAsync(new MethodologyTemplate(TemplateId));
        var later = new FixedTimeProvider(Now.AddHours(1));

        // Act
        var creationReplay = await new CreateMethodologyTemplateHandler(scenario.Executor, later)
            .HandleAsync(Scenario.Context(create, author), CancellationToken.None);
        var revisionReplay = await new ReviseMethodologyTemplateDraftHandler(scenario.Executor, later)
            .HandleAsync(Scenario.Context(revise, author), CancellationToken.None);
        var reviewReplay = await new ReviewMethodologyTemplateHandler(scenario.Executor, later)
            .HandleAsync(Scenario.Context(review, reviewer), CancellationToken.None);
        var approvalReplay = await new ApproveMethodologyTemplateVersionHandler(scenario.Executor, later)
            .HandleAsync(Scenario.Context(approve, approver), CancellationToken.None);
        var retirementReplay = await new RetireMethodologyTemplateHandler(scenario.Executor, later)
            .HandleAsync(Scenario.Context(retire, approver), CancellationToken.None);
        var changedRevision = await new ReviseMethodologyTemplateDraftHandler(scenario.Executor, later)
            .HandleAsync(Scenario.Context(revise with { ExpectedRevision = 2 }, author), CancellationToken.None);
        var changedActor = await new ReviewMethodologyTemplateHandler(scenario.Executor, later)
            .HandleAsync(Scenario.Context(review, approver), CancellationToken.None);
        var after = await scenario.Reader.HydrateAsync(new MethodologyTemplate(TemplateId));

        // Assert
        Assert.True(creationReplay.IsSuccess, creationReplay.Error?.Message);
        Assert.True(revisionReplay.IsSuccess, revisionReplay.Error?.Message);
        Assert.True(reviewReplay.IsSuccess, reviewReplay.Error?.Message);
        Assert.True(approvalReplay.IsSuccess, approvalReplay.Error?.Message);
        Assert.True(retirementReplay.IsSuccess, retirementReplay.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, changedRevision.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changedActor.Error?.Kind);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldReplayContributionProposalAndDecisionsGivenAcceptedContributionAndLaterClock()
    {
        // Arrange
        await using var scenario = new Scenario();
        var tenant = Uuid.CreateVersion4();
        var contributor = Uuid.CreateVersion4();
        var reviewer = Uuid.CreateVersion4();
        var approver = Uuid.CreateVersion4();
        var propose = new ProposeDeidentifiedTemplateContribution(tenant, Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), 3, new MethodologyTemplateRecordContent("quarterly-review", "control",
                "Review access", "Review access quarterly", []), "Removed client identifiers");
        var review = new ReviewTemplateContribution(Uuid.CreateVersion4(), tenant, propose.ContributionId,
            "approved", "Deidentified");
        var accept = new AcceptTemplateContribution(Uuid.CreateVersion4(), tenant, propose.ContributionId, "Accept");
        Assert.True((await new ProposeDeidentifiedTemplateContributionHandler(scenario.ContributionOperations())
            .HandleAsync(Scenario.Context(propose, contributor), CancellationToken.None)).IsSuccess);
        Assert.True((await new ReviewTemplateContributionHandler(scenario.ContributionOperations())
            .HandleAsync(Scenario.Context(review, reviewer), CancellationToken.None)).IsSuccess);
        Assert.True((await new AcceptTemplateContributionHandler(scenario.ContributionOperations())
            .HandleAsync(Scenario.Context(accept, approver), CancellationToken.None)).IsSuccess);
        var before = await scenario.Reader.HydrateAsync(new MethodologyTemplateContribution(tenant,
            propose.ContributionId));
        var later = new FixedTimeProvider(Now.AddHours(1));

        // Act
        var proposalReplay = await new ProposeDeidentifiedTemplateContributionHandler(scenario.ContributionOperations(later))
            .HandleAsync(Scenario.Context(propose, contributor), CancellationToken.None);
        var reviewReplay = await new ReviewTemplateContributionHandler(scenario.ContributionOperations(later))
            .HandleAsync(Scenario.Context(review, reviewer), CancellationToken.None);
        var acceptanceReplay = await new AcceptTemplateContributionHandler(scenario.ContributionOperations(later))
            .HandleAsync(Scenario.Context(accept, approver), CancellationToken.None);
        var changed = await new AcceptTemplateContributionHandler(scenario.ContributionOperations(later))
            .HandleAsync(Scenario.Context(accept with { AcceptanceRationale = "Different" }, approver), CancellationToken.None);
        var after = await scenario.Reader.HydrateAsync(new MethodologyTemplateContribution(tenant,
            propose.ContributionId));

        // Assert
        Assert.True(proposalReplay.IsSuccess, proposalReplay.Error?.Message);
        Assert.True(reviewReplay.IsSuccess, reviewReplay.Error?.Message);
        Assert.True(acceptanceReplay.IsSuccess, acceptanceReplay.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
        var beforeShare = await scenario.Contributions.ShareAsync(tenant, propose.ContributionId);
        var afterShare = await scenario.Contributions.ShareAsync(tenant, propose.ContributionId);
        Assert.Equal(beforeShare!.AcceptanceId, afterShare!.AcceptanceId);
        Assert.Equal(beforeShare.ContentSha256, afterShare.ContentSha256);
    }

    [Fact]
    public async Task ShouldCreateReviewAndPublishVersionGivenIndependentTemplateActors()
    {
        // Arrange
        await using var scenario = new Scenario();
        var author = Uuid.CreateVersion4();
        var reviewer = Uuid.CreateVersion4();
        var approver = Uuid.CreateVersion4();
        var create = new CreateMethodologyTemplateHandler(scenario.Executor, scenario.Clock);
        var revise = new ReviseMethodologyTemplateDraftHandler(scenario.Executor, scenario.Clock);
        var review = new ReviewMethodologyTemplateHandler(scenario.Executor, scenario.Clock);
        var approve = new ApproveMethodologyTemplateVersionHandler(scenario.Executor, scenario.Clock);
        var retire = new RetireMethodologyTemplateHandler(scenario.Executor, scenario.Clock);

        // Act
        var created = await create.HandleAsync(Scenario.Context(
            new CreateMethodologyTemplate(TemplateId, Content("Initial procedure."), "Initial draft"), author),
            CancellationToken.None);
        var revised = await revise.HandleAsync(Scenario.Context(
            new ReviseMethodologyTemplateDraft(Uuid.CreateVersion4(), TemplateId, 1, Content("Revised procedure."), "Clarify control steps"), author),
            CancellationToken.None);
        var reviewed = await review.HandleAsync(Scenario.Context(
            new ReviewMethodologyTemplate(Uuid.CreateVersion4(), TemplateId, 2, "approved", "The procedure is clear and testable."), reviewer),
            CancellationToken.None);
        var published = await approve.HandleAsync(Scenario.Context(
            new ApproveMethodologyTemplateVersion(Uuid.CreateVersion4(), TemplateId, 2, "Approved for firm-wide use."), approver),
            CancellationToken.None);
        var retired = await retire.HandleAsync(Scenario.Context(new RetireMethodologyTemplate(Uuid.CreateVersion4(),
            TemplateId, 4, new DateOnly(2026, 10, 31), "Replaced by the current methodology."), approver),
            CancellationToken.None);
        var aggregate = await scenario.Reader.HydrateAsync(new MethodologyTemplate(TemplateId));

        // Assert
        Assert.True(created.IsSuccess, created.Error?.Message);
        Assert.True(revised.IsSuccess, revised.Error?.Message);
        Assert.True(reviewed.IsSuccess, reviewed.Error?.Message);
        Assert.True(published.IsSuccess, published.Error?.Message);
        Assert.True(retired.IsSuccess, retired.Error?.Message);
        Assert.Equal("retired", aggregate.Current!.Status);
        Assert.Equal(new DateOnly(2026, 10, 31), aggregate.Current.RetiredEffectiveOn);
        var publishedVersion = aggregate.Current.Versions[^1];
        Assert.Equal("published", publishedVersion.Status);
        Assert.Equal(Content("Revised procedure.").Records[0].Summary, publishedVersion.Content.Records[0].Summary);
        var decision = Assert.IsType<MethodologyTemplateReviewView>(publishedVersion.Review);
        var approvedBy = Assert.IsType<Uuid>(publishedVersion.ApprovedByMemberId);
        Assert.NotEqual(publishedVersion.AuthoredByMemberId, decision.ReviewedByMemberId);
        Assert.NotEqual(publishedVersion.AuthoredByMemberId, approvedBy);
        Assert.NotEqual(decision.ReviewedByMemberId, approvedBy);
    }

    [Fact]
    public async Task ShouldAcceptOnlyIndependentlyReviewedDeidentifiedContributionGivenTenantProposal()
    {
        // Arrange
        await using var scenario = new Scenario();
        var tenantId = Uuid.CreateVersion4();
        var contributionId = Uuid.CreateVersion4();
        var sourceRecordId = Uuid.CreateVersion4();
        var contributor = Uuid.CreateVersion4();
        var reviewer = Uuid.CreateVersion4();
        var platformOperator = Uuid.CreateVersion4();
        var propose = new ProposeDeidentifiedTemplateContributionHandler(scenario.ContributionOperations());
        var review = new ReviewTemplateContributionHandler(scenario.ContributionOperations());
        var accept = new AcceptTemplateContributionHandler(scenario.ContributionOperations());
        var content = new MethodologyTemplateRecordContent("quarterly-access-review", "control",
            "Review privileged access", "Review privileged access quarterly.", []);

        // Act
        var proposed = await propose.HandleAsync(Scenario.Context(new ProposeDeidentifiedTemplateContribution(
            tenantId, contributionId, sourceRecordId, 3, content, "Removed client names and system identifiers."), contributor),
            CancellationToken.None);
        var reviewed = await review.HandleAsync(Scenario.Context(new ReviewTemplateContribution(Uuid.CreateVersion4(),
            tenantId, contributionId, "approved", "No tenant identifiers remain."), reviewer), CancellationToken.None);
        var accepted = await accept.HandleAsync(Scenario.Context(new AcceptTemplateContribution(Uuid.CreateVersion4(),
            tenantId, contributionId, "Approved for firm methodology reuse."), platformOperator), CancellationToken.None);
        var aggregate = await scenario.Contributions.ReadAsync(tenantId, contributionId);

        // Assert
        Assert.True(proposed.IsSuccess, proposed.Error?.Message);
        Assert.True(reviewed.IsSuccess, reviewed.Error?.Message);
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        var share = Assert.IsType<MethodologyTemplateContributionShareView>(
            await scenario.Contributions.ShareAsync(tenantId, contributionId));
        Assert.Equal("accepted", aggregate!.Status);
        Assert.NotEqual(aggregate.ProposedByMemberId, share.Review.ReviewedByMemberId);
        Assert.NotEqual(aggregate.ProposedByMemberId, share.AcceptedByMemberId);
        Assert.NotEqual(share.Review.ReviewedByMemberId, share.AcceptedByMemberId);
        Assert.Equal("quarterly-access-review", share.Content.EntryKey);
    }

    [Fact]
    public async Task ShouldPinClientDraftsAndRecordReviewedSuccessorDispositionGivenPublishedTemplateUpdate()
    {
        // Arrange
        await using var scenario = new Scenario();
        var tenantId = Uuid.CreateVersion4();
        var applicationKey = "annual-control-baseline";
        var author = Uuid.CreateVersion4();
        var reviewer = Uuid.CreateVersion4();
        var approver = Uuid.CreateVersion4();
        var clientAdmin = Uuid.CreateVersion4();
        var createProgram = await new CreateProgramHandler(scenario.Executor, scenario.Clock).HandleAsync(
            Scenario.Context(new CreateProgram(tenantId, "Northwind SOC 2", new ProgramPlan(null, null, null,
                null, null, null)), clientAdmin), CancellationToken.None);
        var programId = createProgram.Value!.ProgramId;
        var createTemplate = new CreateMethodologyTemplateHandler(scenario.Executor, scenario.Clock);
        var reviseTemplate = new ReviseMethodologyTemplateDraftHandler(scenario.Executor, scenario.Clock);
        var reviewTemplate = new ReviewMethodologyTemplateHandler(scenario.Executor, scenario.Clock);
        var approveTemplate = new ApproveMethodologyTemplateVersionHandler(scenario.Executor, scenario.Clock);
        var applyTemplate = new ApplyMethodologyTemplateHandler(scenario.Executor, scenario.Reader, scenario.Clock);
        var proposeSuccessor = new ProposeTemplateSuccessorAdoptionHandler(scenario.Executor, scenario.Reader,
            scenario.Clock);
        var recordDisposition = new RecordTemplateSuccessorDispositionHandler(scenario.Executor, scenario.Clock);

        // Act
        var created = await createTemplate.HandleAsync(Scenario.Context(new CreateMethodologyTemplate(
            TemplateId, Content("Initial published procedure."), "Initial release"), author), CancellationToken.None);
        var reviewedFirst = await reviewTemplate.HandleAsync(Scenario.Context(new ReviewMethodologyTemplate(Uuid.CreateVersion4(),
            TemplateId, 1, "approved", "Initial draft is ready."), reviewer), CancellationToken.None);
        var approvedFirst = await approveTemplate.HandleAsync(Scenario.Context(new ApproveMethodologyTemplateVersion(Uuid.CreateVersion4(),
            TemplateId, 1, "Initial release approved."), approver), CancellationToken.None);
        var applied = await applyTemplate.HandleAsync(Scenario.Context(new ApplyMethodologyTemplate(tenantId,
            programId, TemplateId, 1, applicationKey), clientAdmin), CancellationToken.None);
        var afterApply = await scenario.Reader.HydrateAsync(new TemplateApplication(tenantId, programId,
            ApplicationIdFor(programId, applicationKey)));
        var revised = await reviseTemplate.HandleAsync(Scenario.Context(new ReviseMethodologyTemplateDraft(Uuid.CreateVersion4(),
            TemplateId, 3, Content("Updated successor procedure."), "Update annual testing steps"), author),
            CancellationToken.None);
        var reviewedSecond = await reviewTemplate.HandleAsync(Scenario.Context(new ReviewMethodologyTemplate(Uuid.CreateVersion4(),
            TemplateId, 2, "approved", "Updated steps are clear."), reviewer), CancellationToken.None);
        var approvedSecond = await approveTemplate.HandleAsync(Scenario.Context(new ApproveMethodologyTemplateVersion(Uuid.CreateVersion4(),
            TemplateId, 2, "Successor approved for reuse."), approver), CancellationToken.None);
        var proposed = await proposeSuccessor.HandleAsync(Scenario.Context(new ProposeTemplateSuccessorAdoption(Uuid.CreateVersion4(),
            tenantId, programId, TemplateId, applicationKey, 1, 2, "adapt"), clientAdmin), CancellationToken.None);
        var recorded = await recordDisposition.HandleAsync(Scenario.Context(new RecordTemplateSuccessorDisposition(Uuid.CreateVersion4(),
            tenantId, programId, TemplateId, applicationKey, 2, 2, "adopt",
            "The updated procedure fits our existing quarterly control."), clientAdmin), CancellationToken.None);
        var application = await scenario.Reader.HydrateAsync(new TemplateApplication(tenantId, programId,
            ApplicationIdFor(programId, applicationKey)));

        // Assert
        Assert.True(createProgram.IsSuccess, createProgram.Error?.Message);
        Assert.True(created.IsSuccess, created.Error?.Message);
        Assert.True(reviewedFirst.IsSuccess, reviewedFirst.Error?.Message);
        Assert.True(approvedFirst.IsSuccess, approvedFirst.Error?.Message);
        Assert.True(applied.IsSuccess, applied.Error?.Message);
        Assert.True(revised.IsSuccess, revised.Error?.Message);
        Assert.True(reviewedSecond.IsSuccess, reviewedSecond.Error?.Message);
        Assert.True(approvedSecond.IsSuccess, approvedSecond.Error?.Message);
        Assert.True(proposed.IsSuccess, proposed.Error?.Message);
        Assert.True(recorded.IsSuccess, recorded.Error?.Message);
        Assert.Equal(1, afterApply.Current!.TemplateVersionNumber);
        Assert.All(afterApply.Current.Drafts, draft => Assert.Equal("draft_pending", draft.Status));
        Assert.False(afterApply.Current.IsActivated);
        var successor = Assert.Single(application.Current!.SuccessorProposals);
        Assert.Equal(2, successor.Version.VersionNumber);
        Assert.Equal("adapt", successor.ProposedDisposition);
        var decision = Assert.Single(application.Current.SuccessorDecisions);
        Assert.Equal("adopt", decision.Disposition);
        Assert.Equal(1, application.Current.TemplateVersionNumber);
    }

    static MethodologyTemplateContent Content(string summary) => new(
        "SOC 2 starter methodology", "Firm-authored procedures for client-owned drafts.", "soc2",
        [new("cc6-1-access-review", "control", "Review privileged access", summary, [])]);

    static Uuid ApplicationIdFor(Uuid programId, string applicationKey) => Uuid.CreateVersion5(programId,
        $"methodology-template-application:{TemplateId}:{applicationKey}");

    sealed class Scenario : IAsyncDisposable
    {
        readonly ServiceProvider _services;
        readonly AsyncServiceScope _scope;

        public Scenario()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddPortia();
            _services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _services.CreateAsyncScope();
            Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        }

        public TimeProvider Clock { get; } = new FixedTimeProvider(Now);
        public IImmutableContentStore ContentStore { get; } = new FitzKvImmutableContentStore(
            new PersistenceKvClient(new InMemoryKvClient()));
        public MethodologyTemplateContributionReader Contributions => new(Reader, ContentStore);
        public MethodologyTemplateContributionOperations ContributionOperations(TimeProvider? clock = null) =>
            new(new NativeOperationExecutor(Reader, Executor, ContentStore), ContentStore, clock ?? Clock);
        public IAggregateExecutor Executor { get; }
        public IAggregateReader Reader { get; }

        public static RequestContext<TRequest> Context<TRequest>(TRequest request, Uuid userId)
            where TRequest : IRequestBase => new(request, Actor(userId));

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _services.DisposeAsync();
        }

        static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()), new Claim("name", "Test user")],
            "UnitTest"));
    }

    sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
