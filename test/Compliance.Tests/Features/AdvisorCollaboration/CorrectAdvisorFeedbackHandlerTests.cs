using System.Security.Claims;
using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AdvisorCollaboration;

public sealed class CorrectAdvisorFeedbackHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldRecordLinkedCorrectionGivenPersonalHttpActor()
    {
        // Arrange
        await using var fixture = new Fixture();
        var (handoffId, feedbackId, correctionId) = await fixture.SeedHandoffWithFeedbackAsync();
        var request = new CorrectAdvisorFeedback(fixture.TenantId, fixture.ProgramId,
            handoffId, feedbackId, correctionId, "Please identify the review owner.",
            "The original wording was incomplete.");

        // Act
        var result = await new CorrectAdvisorFeedbackHandler(fixture.Executor, fixture.Clock)
            .HandleAsync(Fixture.Context(fixture.UserId, request), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AdvisorHandoff(fixture.TenantId,
            fixture.ProgramId, handoffId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var feedback = Assert.Single(saved.ReadFeedback());
        var correction = Assert.Single(feedback.Corrections!);
        Assert.Equal(correctionId, correction.CorrectionId);
        Assert.Equal("Please identify the review owner.", correction.Body);
        Assert.Equal("The original wording was incomplete.", correction.Reason);
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.UserId).ToString(),
            correction.Actor.Id);
        Assert.Equal(Now.AddMinutes(2), correction.CorrectedAt);
        Assert.Equal(4, saved.Revision);
    }

    [Fact]
    public async Task ShouldNotCorrectFeedbackGivenDifferentProgramScope()
    {
        // Arrange
        await using var fixture = new Fixture();
        var (handoffId, feedbackId, correctionId) = await fixture.SeedHandoffWithFeedbackAsync();
        var request = new CorrectAdvisorFeedback(fixture.TenantId, Uuid.CreateVersion4(),
            handoffId, feedbackId, correctionId, "Changed wording.", "Correction reason.");

        // Act
        var result = await new CorrectAdvisorFeedbackHandler(fixture.Executor, fixture.Clock)
            .HandleAsync(Fixture.Context(fixture.UserId, request), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AdvisorHandoff(fixture.TenantId,
            fixture.ProgramId, handoffId));

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Empty(Assert.Single(saved.ReadFeedback()).Corrections ?? []);
        Assert.Equal(3, saved.Revision);
    }

    [Fact]
    public async Task ShouldRejectSystemDispatchGivenFeedbackCorrection()
    {
        // Arrange
        await using var fixture = new Fixture();
        var (handoffId, feedbackId, correctionId) = await fixture.SeedHandoffWithFeedbackAsync();
        var request = new CorrectAdvisorFeedback(fixture.TenantId, fixture.ProgramId,
            handoffId, feedbackId, correctionId, "Changed wording.", "Correction reason.");

        // Act
        var result = await new CorrectAdvisorFeedbackHandler(fixture.Executor, fixture.Clock)
            .HandleAsync(new RequestContext<CorrectAdvisorFeedback>(request,
                RequestActor.System), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AdvisorHandoff(fixture.TenantId,
            fixture.ProgramId, handoffId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Empty(Assert.Single(saved.ReadFeedback()).Corrections ?? []);
        Assert.Equal(3, saved.Revision);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public Uuid HandoffId { get; } = Uuid.CreateVersion4();
        public Uuid FeedbackId { get; } = Uuid.CreateVersion4();
        public Uuid CorrectionId { get; } = Uuid.CreateVersion4();
        public Uuid RecordId { get; } = Uuid.CreateVersion4();
        public Uuid RecordVersionId { get; } = Uuid.CreateVersion4();
        public TimeProvider Clock { get; } = new FixedTimeProvider(Now.AddMinutes(2));
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }
        public IAggregateExecutor Executor { get; }

        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddPortia();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
            });
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        }

        public async Task<(Uuid HandoffId, Uuid FeedbackId, Uuid CorrectionId)>
            SeedHandoffWithFeedbackAsync()
        {
            var lead = ActorReference.ForMember(RbacIds.Member(TenantId, Uuid.CreateVersion4()),
                "Compliance lead");
            var author = ActorReference.ForMember(RbacIds.Member(TenantId, UserId), "Assigned Advisor");
            var handoff = new AdvisorHandoff(TenantId, ProgramId, HandoffId);
            Assert.True(handoff.Create("Type I controls", [new(RecordId, RecordVersionId)],
                new DateOnly(2026, 12, 31), lead, Now).IsSuccess);
            Assert.True(handoff.MarkReadyForReview(1, lead, Now.AddMinutes(1)).IsSuccess);
            Assert.True(handoff.RecordFeedback(FeedbackId, RecordId, RecordVersionId,
                "change_request", "Clarify the evidence owner.", author, Now.AddMinutes(2)).IsSuccess);
            await Writer.SaveAsync(handoff, new RequestDispatchContext(RequestActor.System));
            return (HandoffId, FeedbackId, CorrectionId);
        }

        public static Cntryl.Portia.RequestContext<T> Context<T>(Uuid userId, T request) where T : IRequestBase =>
                new(request, new RequestDispatchContext(Actor(userId),
                    new HttpInvocation("POST", "/api/v1/advisor-handoffs/feedback/corrections",
                        "/api/v1/advisor-handoffs/feedback/corrections", "advisor-feedback-correction-test")));

        static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()),
                new Claim("email", "advisor@example.test")], "BdgrzSession"));

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
