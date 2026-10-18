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
            new ReviseMethodologyTemplateDraft(TemplateId, 1, Content("Revised procedure."), "Clarify control steps"), author),
            CancellationToken.None);
        var reviewed = await review.HandleAsync(Scenario.Context(
            new ReviewMethodologyTemplate(TemplateId, 2, "approved", "The procedure is clear and testable."), reviewer),
            CancellationToken.None);
        var published = await approve.HandleAsync(Scenario.Context(
            new ApproveMethodologyTemplateVersion(TemplateId, 2, "Approved for firm-wide use."), approver),
            CancellationToken.None);
        var retired = await retire.HandleAsync(Scenario.Context(new RetireMethodologyTemplate(
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
        var propose = new ProposeDeidentifiedTemplateContributionHandler(scenario.Executor, scenario.Clock);
        var review = new ReviewTemplateContributionHandler(scenario.Executor, scenario.Clock);
        var accept = new AcceptTemplateContributionHandler(scenario.Executor, scenario.Clock);
        var content = new MethodologyTemplateRecordContent("quarterly-access-review", "control",
            "Review privileged access", "Review privileged access quarterly.", []);

        // Act
        var proposed = await propose.HandleAsync(Scenario.Context(new ProposeDeidentifiedTemplateContribution(
            tenantId, contributionId, sourceRecordId, 3, content, "Removed client names and system identifiers."), contributor),
            CancellationToken.None);
        var reviewed = await review.HandleAsync(Scenario.Context(new ReviewTemplateContribution(
            tenantId, contributionId, "approved", "No tenant identifiers remain."), reviewer), CancellationToken.None);
        var accepted = await accept.HandleAsync(Scenario.Context(new AcceptTemplateContribution(
            tenantId, contributionId, "Approved for firm methodology reuse."), platformOperator), CancellationToken.None);
        var aggregate = await scenario.Reader.HydrateAsync(new MethodologyTemplateContribution(tenantId, contributionId));

        // Assert
        Assert.True(proposed.IsSuccess, proposed.Error?.Message);
        Assert.True(reviewed.IsSuccess, reviewed.Error?.Message);
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        var share = Assert.IsType<MethodologyTemplateContributionShareView>(aggregate.ShareableContent);
        Assert.Equal("accepted", aggregate.Current!.Status);
        Assert.NotEqual(aggregate.Current.ProposedByMemberId, share.Review.ReviewedByMemberId);
        Assert.NotEqual(aggregate.Current.ProposedByMemberId, share.AcceptedByMemberId);
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
        var reviewedFirst = await reviewTemplate.HandleAsync(Scenario.Context(new ReviewMethodologyTemplate(
            TemplateId, 1, "approved", "Initial draft is ready."), reviewer), CancellationToken.None);
        var approvedFirst = await approveTemplate.HandleAsync(Scenario.Context(new ApproveMethodologyTemplateVersion(
            TemplateId, 1, "Initial release approved."), approver), CancellationToken.None);
        var applied = await applyTemplate.HandleAsync(Scenario.Context(new ApplyMethodologyTemplate(tenantId,
            programId, TemplateId, 1, applicationKey), clientAdmin), CancellationToken.None);
        var afterApply = await scenario.Reader.HydrateAsync(new TemplateApplication(tenantId, programId,
            ApplicationIdFor(programId, applicationKey)));
        var revised = await reviseTemplate.HandleAsync(Scenario.Context(new ReviseMethodologyTemplateDraft(
            TemplateId, 3, Content("Updated successor procedure."), "Update annual testing steps"), author),
            CancellationToken.None);
        var reviewedSecond = await reviewTemplate.HandleAsync(Scenario.Context(new ReviewMethodologyTemplate(
            TemplateId, 2, "approved", "Updated steps are clear."), reviewer), CancellationToken.None);
        var approvedSecond = await approveTemplate.HandleAsync(Scenario.Context(new ApproveMethodologyTemplateVersion(
            TemplateId, 2, "Successor approved for reuse."), approver), CancellationToken.None);
        var proposed = await proposeSuccessor.HandleAsync(Scenario.Context(new ProposeTemplateSuccessorAdoption(
            tenantId, programId, TemplateId, applicationKey, 1, 2, "adapt"), clientAdmin), CancellationToken.None);
        var recorded = await recordDisposition.HandleAsync(Scenario.Context(new RecordTemplateSuccessorDisposition(
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
