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
    public async Task ShouldCreateDraftHandoffGivenPersonalHttpActor()
    {
        // Arrange
        await using var fixture = new Fixture();
        var handoffId = Uuid.CreateVersion4();
        var recordId = Uuid.CreateVersion4();
        var versionId = Uuid.CreateVersion4();
        var request = new CreateAdvisorHandoff(fixture.TenantId, fixture.ProgramId, handoffId,
            [new(recordId, versionId)], "  Type I controls  ", new DateOnly(2026, 12, 31));

        // Act
        var result = await new CreateAdvisorHandoffHandler(fixture.Executor, fixture.Resolver,
                fixture.Clock)
            .HandleAsync(Fixture.Context(fixture.UserId, request), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AdvisorHandoff(fixture.TenantId,
            fixture.ProgramId, handoffId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(saved.IsCreated);
        Assert.Equal("draft", saved.Status);
        Assert.Equal("Type I controls", saved.Purpose);
        Assert.Equal(new DateOnly(2026, 12, 31), saved.ExpiresOn);
        Assert.Equal(1, saved.Revision);
        Assert.Equal(Now.AddMinutes(2), saved.CreatedAt);
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.UserId).ToString(),
            saved.CreatedBy!.Id);
        Assert.Equal([new AdvisorRecordVersion(recordId, versionId)], saved.Selection);
    }

    [Fact]
    public async Task ShouldNotCreateHandoffGivenSystemDispatch()
    {
        // Arrange
        await using var fixture = new Fixture();
        var handoffId = Uuid.CreateVersion4();
        var request = new CreateAdvisorHandoff(fixture.TenantId, fixture.ProgramId, handoffId,
            [new(Uuid.CreateVersion4(), Uuid.CreateVersion4())], "Review", new DateOnly(2026, 12, 31));

        // Act
        var result = await new CreateAdvisorHandoffHandler(fixture.Executor, fixture.Resolver,
                fixture.Clock)
            .HandleAsync(new Cntryl.Portia.RequestContext<CreateAdvisorHandoff>(request,
                RequestActor.System), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AdvisorHandoff(fixture.TenantId,
            fixture.ProgramId, handoffId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.False(saved.IsCreated);
    }

    [Fact]
    public async Task ShouldReviseSelectionGivenCurrentHandoffRevision()
    {
        // Arrange
        await using var fixture = new Fixture();
        var (handoffId, _, _) = await fixture.SeedOpenHandoffWithSelectionAsync();
        var revisedRecordId = Uuid.CreateVersion4();
        var revisedVersionId = Uuid.CreateVersion4();
        var request = new ReviseAdvisorHandoffSelection(fixture.TenantId, fixture.ProgramId,
            handoffId, 2, [new(revisedRecordId, revisedVersionId)], "Updated the review scope");

        // Act
        var result = await new ReviseAdvisorHandoffSelectionHandler(fixture.Executor,
                fixture.Resolver, fixture.Clock)
            .HandleAsync(Fixture.Context(fixture.UserId, request), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AdvisorHandoff(fixture.TenantId,
            fixture.ProgramId, handoffId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("draft", saved.Status);
        Assert.Equal(3, saved.Revision);
        Assert.Equal(Now.AddMinutes(2), saved.UpdatedAt);
        Assert.Equal([new AdvisorRecordVersion(revisedRecordId, revisedVersionId)], saved.Selection);
    }

    [Fact]
    public async Task ShouldNotReviseSelectionGivenStaleHandoffRevision()
    {
        // Arrange
        await using var fixture = new Fixture();
        var (handoffId, recordId, versionId) = await fixture.SeedOpenHandoffWithSelectionAsync();
        var request = new ReviseAdvisorHandoffSelection(fixture.TenantId, fixture.ProgramId,
            handoffId, 1, [new(Uuid.CreateVersion4(), Uuid.CreateVersion4())], "Changed selection");

        // Act
        var result = await new ReviseAdvisorHandoffSelectionHandler(fixture.Executor,
                fixture.Resolver, fixture.Clock)
            .HandleAsync(Fixture.Context(fixture.UserId, request), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AdvisorHandoff(fixture.TenantId,
            fixture.ProgramId, handoffId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(2, saved.Revision);
        Assert.Equal([new AdvisorRecordVersion(recordId, versionId)], saved.Selection);
    }

    [Fact]
    public async Task ShouldMarkDraftHandoffReadyGivenCurrentRevision()
    {
        // Arrange
        await using var fixture = new Fixture();
        var handoffId = await fixture.SeedDraftHandoffAsync();
        var request = new MarkAdvisorHandoffReadyForReview(fixture.TenantId, fixture.ProgramId,
            handoffId, 1);

        // Act
        var result = await new MarkAdvisorHandoffReadyForReviewHandler(fixture.Executor,
                fixture.Reader, fixture.Resolver, fixture.Clock)
            .HandleAsync(Fixture.Context(fixture.UserId, request), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AdvisorHandoff(fixture.TenantId,
            fixture.ProgramId, handoffId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("ready_for_review", saved.Status);
        Assert.Equal(2, saved.Revision);
        Assert.Equal(Now.AddMinutes(2), saved.UpdatedAt);
    }

    [Fact]
    public async Task ShouldKeepDraftHandoffClosedGivenAStaleSelectedSource()
    {
        // Arrange
        await using var fixture = new Fixture
        {
            Resolver = new RejectCurrentAdvisorRecordVersions(RequestErrorKind.Conflict),
        };
        var handoffId = await fixture.SeedDraftHandoffAsync();
        var request = new MarkAdvisorHandoffReadyForReview(fixture.TenantId, fixture.ProgramId,
            handoffId, 1);

        // Act
        var result = await new MarkAdvisorHandoffReadyForReviewHandler(fixture.Executor,
                fixture.Reader, fixture.Resolver, fixture.Clock)
            .HandleAsync(Fixture.Context(fixture.UserId, request), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AdvisorHandoff(fixture.TenantId,
            fixture.ProgramId, handoffId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal("draft", saved.Status);
        Assert.Equal(1, saved.Revision);
    }

    [Fact]
    public async Task ShouldNotMarkHandoffReadyGivenSystemDispatch()
    {
        // Arrange
        await using var fixture = new Fixture();
        var handoffId = await fixture.SeedDraftHandoffAsync();
        var request = new MarkAdvisorHandoffReadyForReview(fixture.TenantId, fixture.ProgramId,
            handoffId, 1);

        // Act
        var result = await new MarkAdvisorHandoffReadyForReviewHandler(fixture.Executor,
                fixture.Reader, fixture.Resolver, fixture.Clock)
            .HandleAsync(new Cntryl.Portia.RequestContext<MarkAdvisorHandoffReadyForReview>(request,
                RequestActor.System), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AdvisorHandoff(fixture.TenantId,
            fixture.ProgramId, handoffId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal("draft", saved.Status);
        Assert.Equal(1, saved.Revision);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("revise")]
    [InlineData("ready")]
    public async Task ShouldNotMutateHandoffGivenDifferentProgramScope(string operation)
    {
        // Arrange
        await using var fixture = new Fixture();
        var otherProgramId = Uuid.CreateVersion4();
        var handoffId = operation == "revise"
            ? (await fixture.SeedOpenHandoffWithSelectionAsync()).HandoffId
            : await fixture.SeedDraftHandoffAsync();
        IRequestBase request = operation switch
        {
            "create" => new CreateAdvisorHandoff(fixture.TenantId, otherProgramId, handoffId,
                [new(Uuid.CreateVersion4(), Uuid.CreateVersion4())], "Review",
                new DateOnly(2026, 12, 31)),
            "revise" => new ReviseAdvisorHandoffSelection(fixture.TenantId, otherProgramId,
                handoffId, 2, [new(Uuid.CreateVersion4(), Uuid.CreateVersion4())], "Wrong program"),
            "ready" => new MarkAdvisorHandoffReadyForReview(fixture.TenantId, otherProgramId,
                handoffId, 1),
            _ => throw new InvalidOperationException()
        };

        // Act
        var result = operation switch
        {
            "create" => await new CreateAdvisorHandoffHandler(fixture.Executor, fixture.Resolver,
                fixture.Clock)
                .HandleAsync(Fixture.Context(fixture.UserId, (CreateAdvisorHandoff)request),
                    CancellationToken.None),
            "revise" => await new ReviseAdvisorHandoffSelectionHandler(fixture.Executor,
                fixture.Resolver, fixture.Clock).HandleAsync(Fixture.Context(fixture.UserId,
                    (ReviseAdvisorHandoffSelection)request), CancellationToken.None),
            "ready" => await new MarkAdvisorHandoffReadyForReviewHandler(fixture.Executor,
                fixture.Reader, fixture.Resolver, fixture.Clock).HandleAsync(Fixture.Context(fixture.UserId,
                    (MarkAdvisorHandoffReadyForReview)request), CancellationToken.None),
            _ => throw new InvalidOperationException()
        };
        var saved = await fixture.Reader.HydrateAsync(new AdvisorHandoff(fixture.TenantId,
            fixture.ProgramId, handoffId));

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Equal(operation == "revise" ? "ready_for_review" : "draft", saved.Status);
        Assert.Equal(operation == "revise" ? 2 : 1, saved.Revision);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("revise")]
    [InlineData("ready")]
    public async Task ShouldRejectMcpInvocationGivenPersonalHandoffMutation(string operation)
    {
        // Arrange
        await using var fixture = new Fixture();
        var handoffId = operation == "revise"
            ? (await fixture.SeedOpenHandoffWithSelectionAsync()).HandoffId
            : operation == "ready" ? await fixture.SeedDraftHandoffAsync() : Uuid.CreateVersion4();
        IRequestBase request = operation switch
        {
            "create" => new CreateAdvisorHandoff(fixture.TenantId, fixture.ProgramId, handoffId,
                [new(Uuid.CreateVersion4(), Uuid.CreateVersion4())], "Review",
                new DateOnly(2026, 12, 31)),
            "revise" => new ReviseAdvisorHandoffSelection(fixture.TenantId, fixture.ProgramId,
                handoffId, 2, [new(Uuid.CreateVersion4(), Uuid.CreateVersion4())], "Updated scope"),
            "ready" => new MarkAdvisorHandoffReadyForReview(fixture.TenantId, fixture.ProgramId,
                handoffId, 1),
            _ => throw new InvalidOperationException()
        };
        var dispatch = new RequestDispatchContext(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", fixture.UserId.ToString()),
                new Claim("email", "lead@example.test")], "BdgrzSession")),
            new McpInvocation("advisor-handoff-mutation"));

        // Act
        var result = operation switch
        {
            "create" => await new CreateAdvisorHandoffHandler(fixture.Executor, fixture.Resolver,
                fixture.Clock)
                .HandleAsync(Fixture.Context((CreateAdvisorHandoff)request, dispatch),
                    CancellationToken.None),
            "revise" => await new ReviseAdvisorHandoffSelectionHandler(fixture.Executor,
                fixture.Resolver, fixture.Clock).HandleAsync(Fixture.Context(
                    (ReviseAdvisorHandoffSelection)request, dispatch), CancellationToken.None),
            "ready" => await new MarkAdvisorHandoffReadyForReviewHandler(fixture.Executor,
                fixture.Reader, fixture.Resolver, fixture.Clock).HandleAsync(Fixture.Context(
                    (MarkAdvisorHandoffReadyForReview)request, dispatch), CancellationToken.None),
            _ => throw new InvalidOperationException()
        };
        var saved = await fixture.Reader.HydrateAsync(new AdvisorHandoff(fixture.TenantId,
            fixture.ProgramId, handoffId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(operation == "create" ? "missing" :
            operation == "revise" ? "ready_for_review" : "draft", saved.Status);
        Assert.Equal(operation == "revise" ? 2 : operation == "ready" ? 1 : 0,
            saved.Revision);
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
    public async Task ShouldRecordFeedbackAgainstNativePolicyVersionGivenSelectedSource()
    {
        // Arrange
        await using var fixture = new Fixture();
        var handoffId = Uuid.CreateVersion4();
        var feedbackId = Uuid.CreateVersion4();
        var selected = AdvisorRecordVersion.PolicyVersion(Uuid.CreateVersion4(), 2, 4);
        var handoff = new AdvisorHandoff(fixture.TenantId, fixture.ProgramId, handoffId);
        var lead = ActorReference.ForMember(RbacIds.Member(fixture.TenantId, fixture.UserId),
            "Compliance lead");
        Assert.True(handoff.Create("Review policy", [selected], new DateOnly(2026, 12, 31),
            lead, Now).IsSuccess);
        Assert.True(handoff.MarkReadyForReview(1, lead, Now.AddMinutes(1)).IsSuccess);
        await fixture.Writer.SaveAsync(handoff, new RequestDispatchContext(RequestActor.System));
        var request = new RecordAdvisorFeedback(fixture.TenantId, fixture.ProgramId, handoffId,
            feedbackId, selected.RecordId, selected.VersionId, "comment",
            "Clarify the policy owner.")
        {
            SelectedSource = selected,
        };

        // Act
        var result = await new RecordAdvisorFeedbackHandler(fixture.Executor, fixture.Clock)
            .HandleAsync(Fixture.Context(fixture.UserId, request), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AdvisorHandoff(fixture.TenantId,
            fixture.ProgramId, handoffId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(selected, Assert.Single(saved.ReadFeedback()).SourceVersion);
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

    [Fact]
    public async Task ShouldResolveFeedbackOnceGivenPersonalHttpActor()
    {
        // Arrange
        await using var fixture = new Fixture();
        var (handoffId, feedbackId) = await fixture.SeedHandoffWithFeedbackAsync();
        var request = new ResolveAdvisorFeedback(fixture.TenantId, fixture.ProgramId,
            handoffId, feedbackId);
        var handler = new ResolveAdvisorFeedbackHandler(fixture.Executor, fixture.Clock);

        // Act
        var first = await handler.HandleAsync(Fixture.Context(fixture.UserId, request),
            CancellationToken.None);
        var retry = await handler.HandleAsync(Fixture.Context(fixture.UserId, request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AdvisorHandoff(fixture.TenantId,
            fixture.ProgramId, handoffId));
        var listed = await new ListAdvisorFeedbackHandler(fixture.Reader,
                new AcceptCurrentAdvisorRecordVersions())
            .HandleAsync(new Cntryl.Portia.RequestContext<ListAdvisorFeedback>(
                new ListAdvisorFeedback(fixture.TenantId, fixture.ProgramId, handoffId),
                RequestActor.System), CancellationToken.None);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        var feedback = Assert.Single(saved.ReadFeedback());
        Assert.Equal(Now.AddMinutes(2), feedback.ResolvedAt);
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.UserId).ToString(),
            feedback.ResolvedBy?.Id);
        Assert.Equal(4, saved.Revision);
        Assert.True(listed.IsSuccess, listed.Error?.Message);
        Assert.Equal(feedback.ResolvedBy, Assert.Single(listed.Value.Items).ResolvedBy);
    }

    [Fact]
    public async Task ShouldRejectSystemDispatchGivenAdvisorFeedbackResolution()
    {
        // Arrange
        await using var fixture = new Fixture();
        var (handoffId, feedbackId) = await fixture.SeedHandoffWithFeedbackAsync();
        var request = new ResolveAdvisorFeedback(fixture.TenantId, fixture.ProgramId,
            handoffId, feedbackId);

        // Act
        var result = await new ResolveAdvisorFeedbackHandler(fixture.Executor, fixture.Clock)
            .HandleAsync(new Cntryl.Portia.RequestContext<ResolveAdvisorFeedback>(request,
                RequestActor.System), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AdvisorHandoff(fixture.TenantId,
            fixture.ProgramId, handoffId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Null(Assert.Single(saved.ReadFeedback()).ResolvedAt);
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
        public IAdvisorRecordVersionResolver Resolver { get; set; } = new AcceptCurrentAdvisorRecordVersions();

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

        public async Task<Uuid> SeedDraftHandoffAsync()
        {
            var handoffId = Uuid.CreateVersion4();
            var handoff = new AdvisorHandoff(TenantId, ProgramId, handoffId);
            Assert.True(handoff.Create("Type I controls",
                [new(Uuid.CreateVersion4(), Uuid.CreateVersion4())],
                new DateOnly(2026, 12, 31), ActorReference.ForMember(RbacIds.Member(TenantId,
                    UserId), "Compliance lead"), Now).IsSuccess);
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

        public static Cntryl.Portia.RequestContext<T> Context<T>(T request,
            RequestDispatchContext dispatch) where T : IRequestBase => new(request, dispatch);

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

    sealed class AcceptCurrentAdvisorRecordVersions : IAdvisorRecordVersionResolver
    {
        public ValueTask<Result> RequireCurrentAsync(Uuid tenantId, Uuid programId,
            IReadOnlyList<AdvisorRecordVersion> selection, CancellationToken ct = default) =>
            ValueTask.FromResult(Result.Success);
    }

    sealed class RejectCurrentAdvisorRecordVersions(RequestErrorKind kind)
        : IAdvisorRecordVersionResolver
    {
        public ValueTask<Result> RequireCurrentAsync(Uuid tenantId, Uuid programId,
            IReadOnlyList<AdvisorRecordVersion> selection, CancellationToken ct = default) =>
            ValueTask.FromResult(Result.Failure(new RequestError(kind,
                "A selected source version is no longer current.")));
    }
}
