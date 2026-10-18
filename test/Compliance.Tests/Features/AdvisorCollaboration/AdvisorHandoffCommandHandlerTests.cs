using System.Security.Claims;
using System.Globalization;
using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AdvisorCollaboration;

public sealed class AdvisorHandoffCommandHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(false, "closed")]
    [InlineData(true, "revoked")]
    public async Task ShouldEndOpenHandoffGivenAuthorizedManagementCommand(bool revoke, string status)
    {
        // Arrange
        await using var fixture = new Fixture();
        var handoffId = await fixture.SeedOpenHandoffAsync();
        var request = revoke
            ? (IRequestBase)new RevokeAdvisorHandoff(fixture.TenantId, fixture.ProgramId,
                handoffId, "The review is no longer required")
            : new CloseAdvisorHandoff(fixture.TenantId, fixture.ProgramId,
                handoffId, "The review is complete");

        // Act
        var result = revoke
            ? await new RevokeAdvisorHandoffHandler(fixture.Executor, fixture.Clock)
                .HandleAsync(Fixture.Context(fixture.UserId, (RevokeAdvisorHandoff)request),
                    CancellationToken.None)
            : await new CloseAdvisorHandoffHandler(fixture.Executor, fixture.Clock)
                .HandleAsync(Fixture.Context(fixture.UserId, (CloseAdvisorHandoff)request),
                    CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AdvisorHandoff(fixture.TenantId,
            fixture.ProgramId, handoffId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(status, saved.Status);
        Assert.Equal(3, saved.Revision);
    }

    [Fact]
    public async Task ShouldRecordFeedbackAgainstSharedExactSelectionGivenPersonalHttpActor()
    {
        // Arrange
        await using var fixture = new Fixture();
        var (handoffId, recordId, recordVersionId) = await fixture.SeedOpenHandoffWithSelectionAsync();
        var feedbackId = Uuid.CreateVersion4();
        var request = new RecordAdvisorFeedback(fixture.TenantId, fixture.ProgramId,
            handoffId, feedbackId, recordId, recordVersionId, "change_request",
            "Clarify the evidence owner.");

        // Act
        var result = await new RecordAdvisorFeedbackHandler(fixture.Executor, fixture.Clock)
            .HandleAsync(Fixture.Context(fixture.UserId, request), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AdvisorHandoff(fixture.TenantId,
            fixture.ProgramId, handoffId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var feedback = Assert.Single(saved.ReadFeedback());
        Assert.Equal(feedbackId, feedback.FeedbackId);
        Assert.Equal(recordId, feedback.RecordId);
        Assert.Equal(recordVersionId, feedback.RecordVersionId);
        Assert.Equal("change_request", feedback.Kind);
        Assert.Equal("Clarify the evidence owner.", feedback.Body);
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.UserId).ToString(), feedback.Author.Id);
        Assert.Equal(Now.AddMinutes(2), feedback.CreatedAt);
        Assert.Equal(3, saved.Revision);

        var retry = await new RecordAdvisorFeedbackHandler(fixture.Executor, fixture.Clock)
            .HandleAsync(Fixture.Context(fixture.UserId, request), CancellationToken.None);
        var retried = await fixture.Reader.HydrateAsync(new AdvisorHandoff(fixture.TenantId,
            fixture.ProgramId, handoffId));
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Single(retried.ReadFeedback());
        Assert.Equal(3, retried.Revision);
    }

    [Fact]
    public async Task ShouldRejectSystemDispatchGivenAdvisorFeedback()
    {
        // Arrange
        await using var fixture = new Fixture();
        var (handoffId, recordId, recordVersionId) = await fixture.SeedOpenHandoffWithSelectionAsync();
        var request = new RecordAdvisorFeedback(fixture.TenantId, fixture.ProgramId,
            handoffId, Uuid.CreateVersion4(), recordId, recordVersionId, "comment",
            "Please clarify the control owner.");

        // Act
        var result = await new RecordAdvisorFeedbackHandler(fixture.Executor, fixture.Clock)
            .HandleAsync(new Cntryl.Portia.RequestContext<RecordAdvisorFeedback>(request,
                RequestActor.System), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AdvisorHandoff(fixture.TenantId,
            fixture.ProgramId, handoffId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Empty(saved.ReadFeedback());
        Assert.Equal(2, saved.Revision);
    }

    [Fact]
    public async Task ShouldRejectFeedbackGivenTargetOutsideSharedExactSelection()
    {
        // Arrange
        await using var fixture = new Fixture();
        var (handoffId, _, _) = await fixture.SeedOpenHandoffWithSelectionAsync();
        var request = new RecordAdvisorFeedback(fixture.TenantId, fixture.ProgramId,
            handoffId, Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            "comment", "Please clarify the control owner.");

        // Act
        var result = await new RecordAdvisorFeedbackHandler(fixture.Executor, fixture.Clock)
            .HandleAsync(Fixture.Context(fixture.UserId, request), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AdvisorHandoff(fixture.TenantId,
            fixture.ProgramId, handoffId));

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(saved.ReadFeedback());
        Assert.Equal(2, saved.Revision);
    }

    [Fact]
    public async Task ShouldNotRecordFeedbackGivenDifferentProgramScope()
    {
        // Arrange
        await using var fixture = new Fixture();
        var (handoffId, recordId, recordVersionId) = await fixture.SeedOpenHandoffWithSelectionAsync();
        var request = new RecordAdvisorFeedback(fixture.TenantId, Uuid.CreateVersion4(),
            handoffId, Uuid.CreateVersion4(), recordId, recordVersionId, "comment",
            "Please clarify the control owner.");

        // Act
        var result = await new RecordAdvisorFeedbackHandler(fixture.Executor, fixture.Clock)
            .HandleAsync(Fixture.Context(fixture.UserId, request), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AdvisorHandoff(fixture.TenantId,
            fixture.ProgramId, handoffId));

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Empty(saved.ReadFeedback());
        Assert.Equal(2, saved.Revision);
    }

    [Fact]
    public async Task ShouldRejectSystemDispatchGivenHandoffTermination()
    {
        // Arrange
        await using var fixture = new Fixture();
        var handoffId = await fixture.SeedOpenHandoffAsync();
        var request = new CloseAdvisorHandoff(fixture.TenantId, fixture.ProgramId,
            handoffId, "The review is complete");

        // Act
        var result = await new CloseAdvisorHandoffHandler(fixture.Executor, fixture.Clock)
            .HandleAsync(new Cntryl.Portia.RequestContext<CloseAdvisorHandoff>(request,
                RequestActor.System), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AdvisorHandoff(fixture.TenantId,
            fixture.ProgramId, handoffId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal("ready_for_review", saved.Status);
        Assert.Equal(2, saved.Revision);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldNotEndHandoffGivenDifferentProgramScope(bool revoke)
    {
        // Arrange
        await using var fixture = new Fixture();
        var handoffId = await fixture.SeedOpenHandoffAsync();
        var otherProgramId = Uuid.CreateVersion4();

        // Act
        var result = revoke
            ? await new RevokeAdvisorHandoffHandler(fixture.Executor, fixture.Clock)
                .HandleAsync(Fixture.Context(fixture.UserId, new RevokeAdvisorHandoff(
                    fixture.TenantId, otherProgramId, handoffId, "Wrong program")),
                    CancellationToken.None)
            : await new CloseAdvisorHandoffHandler(fixture.Executor, fixture.Clock)
                .HandleAsync(Fixture.Context(fixture.UserId, new CloseAdvisorHandoff(
                    fixture.TenantId, otherProgramId, handoffId, "Wrong program")),
                    CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AdvisorHandoff(fixture.TenantId,
            fixture.ProgramId, handoffId));

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Equal("ready_for_review", saved.Status);
        Assert.Equal(2, saved.Revision);
    }

    [Fact]
    public async Task ShouldRecordClientResponseAgainstExplicitHandoffGivenPersonalHttpActor()
    {
        // Arrange
        await using var fixture = new Fixture();
        var (handoffId, feedbackId) = await fixture.SeedHandoffWithFeedbackAsync();
        var request = new RespondToAdvisorFeedback(fixture.TenantId, fixture.ProgramId,
            handoffId, feedbackId, "We updated the control draft.");

        // Act
        var result = await new RespondToAdvisorFeedbackHandler(fixture.Executor, fixture.Clock)
            .HandleAsync(Fixture.Context(fixture.UserId, request), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AdvisorHandoff(fixture.TenantId,
            fixture.ProgramId, handoffId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var feedback = Assert.Single(saved.ReadFeedback());
        var response = Assert.Single(feedback.Responses!);
        Assert.Equal("We updated the control draft.", response.Body);
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.UserId),
            Uuid.Parse(response.Actor.Id, CultureInfo.InvariantCulture));
        Assert.Equal(Now.AddMinutes(2), response.RespondedAt);
    }

    [Fact]
    public async Task ShouldRejectSystemDispatchGivenAdvisorFeedbackResponse()
    {
        // Arrange
        await using var fixture = new Fixture();
        var (handoffId, feedbackId) = await fixture.SeedHandoffWithFeedbackAsync();
        var request = new RespondToAdvisorFeedback(fixture.TenantId, fixture.ProgramId,
            handoffId, feedbackId, "We updated the control draft.");

        // Act
        var result = await new RespondToAdvisorFeedbackHandler(fixture.Executor, fixture.Clock)
            .HandleAsync(new Cntryl.Portia.RequestContext<RespondToAdvisorFeedback>(request,
                RequestActor.System), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AdvisorHandoff(fixture.TenantId,
            fixture.ProgramId, handoffId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Empty(Assert.Single(saved.ReadFeedback()).Responses ?? []);
        Assert.Equal(3, saved.Revision);
    }

    [Fact]
    public async Task ShouldNotRespondGivenHandoffInDifferentProgramScope()
    {
        // Arrange
        await using var fixture = new Fixture();
        var (handoffId, feedbackId) = await fixture.SeedHandoffWithFeedbackAsync();
        var request = new RespondToAdvisorFeedback(fixture.TenantId, Uuid.CreateVersion4(),
            handoffId, feedbackId, "Response from the wrong program.");

        // Act
        var result = await new RespondToAdvisorFeedbackHandler(fixture.Executor, fixture.Clock)
            .HandleAsync(Fixture.Context(fixture.UserId, request), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AdvisorHandoff(fixture.TenantId,
            fixture.ProgramId, handoffId));

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Empty(Assert.Single(saved.ReadFeedback()).Responses ?? []);
        Assert.Equal(3, saved.Revision);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; } = Uuid.CreateVersion4();
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

        public async Task<Uuid> SeedOpenHandoffAsync()
        {
            var handoffId = Uuid.CreateVersion4();
            var handoff = new AdvisorHandoff(TenantId, ProgramId, handoffId);
            Assert.True(handoff.Create("Type I controls", [new(Uuid.CreateVersion4(), Uuid.CreateVersion4())],
                new DateOnly(2026, 12, 31), ActorReference.ForMember(RbacIds.Member(TenantId, UserId),
                    "Compliance lead"), Now).IsSuccess);
            Assert.True(handoff.MarkReadyForReview(1,
                ActorReference.ForMember(RbacIds.Member(TenantId, UserId), "Compliance lead"),
                Now.AddMinutes(1)).IsSuccess);
            await Writer.SaveAsync(handoff, new RequestDispatchContext(RequestActor.System));
            return handoffId;
        }

        public async Task<(Uuid HandoffId, Uuid RecordId, Uuid RecordVersionId)>
            SeedOpenHandoffWithSelectionAsync()
        {
            var handoffId = Uuid.CreateVersion4();
            var recordId = Uuid.CreateVersion4();
            var recordVersionId = Uuid.CreateVersion4();
            var lead = ActorReference.ForMember(RbacIds.Member(TenantId, UserId), "Compliance lead");
            var handoff = new AdvisorHandoff(TenantId, ProgramId, handoffId);
            Assert.True(handoff.Create("Type I controls", [new(recordId, recordVersionId)],
                new DateOnly(2026, 12, 31), lead, Now).IsSuccess);
            Assert.True(handoff.MarkReadyForReview(1, lead, Now.AddMinutes(1)).IsSuccess);
            await Writer.SaveAsync(handoff, new RequestDispatchContext(RequestActor.System));
            return (handoffId, recordId, recordVersionId);
        }

        public async Task<(Uuid HandoffId, Uuid FeedbackId)> SeedHandoffWithFeedbackAsync()
        {
            var handoffId = Uuid.CreateVersion4();
            var feedbackId = Uuid.CreateVersion4();
            var recordId = Uuid.CreateVersion4();
            var versionId = Uuid.CreateVersion4();
            var handoff = new AdvisorHandoff(TenantId, ProgramId, handoffId);
            var lead = ActorReference.ForMember(RbacIds.Member(TenantId, UserId), "Compliance lead");
            Assert.True(handoff.Create("Type I controls", [new(recordId, versionId)],
                new DateOnly(2026, 12, 31), lead, Now).IsSuccess);
            Assert.True(handoff.MarkReadyForReview(1, lead, Now.AddMinutes(1)).IsSuccess);
            Assert.True(handoff.RecordFeedback(feedbackId, recordId, versionId, "comment",
                "Please clarify the control owner.", ActorReference.ForMember(Uuid.CreateVersion4(),
                    "Advisor"), Now.AddMinutes(2)).IsSuccess);
            await Writer.SaveAsync(handoff, new RequestDispatchContext(RequestActor.System));
            return (handoffId, feedbackId);
        }

        public static Cntryl.Portia.RequestContext<T> Context<T>(Uuid userId, T request)
            where T : IRequestBase => new(request, new RequestDispatchContext(Actor(userId),
                new HttpInvocation("POST", "/api/v1/advisor-handoffs/end",
                    "/api/v1/advisor-handoffs/end", "advisor-handoff-handler-test")));

        static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()),
                new Claim("email", "lead@example.test")], "BdgrzSession"));

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
