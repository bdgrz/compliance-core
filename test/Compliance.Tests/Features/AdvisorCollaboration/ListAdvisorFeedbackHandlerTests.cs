using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AdvisorCollaboration;

public sealed class ListAdvisorFeedbackHandlerTests
{
    static readonly AdvisorRecordVersion First = AdvisorRecordVersion.ControlVersion(Uuid.CreateVersion4(),
        Uuid.CreateVersion4(), 1);

    static readonly AdvisorRecordVersion Second = AdvisorRecordVersion.ControlVersion(Uuid.CreateVersion4(),
        Uuid.CreateVersion4(), 1);

    static ListAdvisorFeedbackHandler Handler(AdvisorHost host, IAdvisorRecordVersionResolver? resolver = null) =>
        new(host.Reader, host.Handoffs, resolver ?? new AcceptCurrent());

    static RequestContext<ListAdvisorFeedback> Context(ListAdvisorFeedback request) =>
        new(request, new ClaimsPrincipal());

    static async Task<(Uuid HandoffId, Uuid FeedbackId, Uuid SecondFeedbackId, ActorReference Advisor)> SeedAsync(
        AdvisorHost host, bool twoEntries)
    {
        var handoffId = Uuid.CreateVersion4();
        var feedbackId = Uuid.CreateVersion4();
        var secondId = Uuid.CreateVersion4();
        var advisorUserId = Uuid.CreateVersion4();
        Assert.True((await host.CreateAsync(handoffId, [First, Second], "Type I readiness review")).IsSuccess);
        Assert.True((await host.ReadyAsync(handoffId, 1)).IsSuccess);
        Assert.True((await host.RecordAsync(host.Feedback(handoffId, feedbackId, First, 2, "Clarify the evidence owner.",
            "change_request"), advisorUserId)).IsSuccess);
        if (twoEntries)
            Assert.True((await host.RecordAsync(host.Feedback(handoffId, secondId, Second, 3, "Add the reviewer role."),
                advisorUserId)).IsSuccess);
        await host.MaterializeAsync(handoffId);
        if (!twoEntries)
        {
            Assert.True((await host.CorrectHandler.HandleAsync(host.Http(new CorrectAdvisorFeedback(host.TenantId,
                host.ProgramId, handoffId, feedbackId, Uuid.CreateVersion4(), "Please explain who reviews the evidence.",
                "I corrected the wording.", 1), advisorUserId), CancellationToken.None)).IsSuccess);
            Assert.True((await host.RespondHandler.HandleAsync(host.Http(new RespondToAdvisorFeedback(
                Uuid.CreateVersion4(), host.TenantId, host.ProgramId, handoffId, feedbackId,
                "We will add the reviewer role.", 2)), CancellationToken.None)).IsSuccess);
        }

        return (handoffId, feedbackId, secondId, ActorReference.ForMember(RbacIds.Member(host.TenantId, advisorUserId),
            "Advisor"));
    }

    [Fact]
    public async Task ShouldReturnAttributedFeedbackHistoryGivenScopedHandoff()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, feedbackId, _, advisor) = await SeedAsync(host, twoEntries: false);

        // Act
        var result = await Handler(host).HandleAsync(Context(new ListAdvisorFeedback(host.TenantId, host.ProgramId,
            handoffId)), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var entry = Assert.Single(result.Value.Items);
        Assert.Equal(feedbackId, entry.AdvisorFeedbackId);
        Assert.Equal(First.RecordId, entry.RecordId);
        Assert.Equal(First.VersionId, entry.RecordVersionId);
        Assert.Equal("change_request", entry.Kind);
        Assert.Equal("Clarify the evidence owner.", entry.Body);
        Assert.Equal(advisor.Id, entry.Author.Id);
        Assert.Equal(AdvisorHost.Start, entry.CreatedAt);
        Assert.Equal("Please explain who reviews the evidence.", Assert.Single(entry.Corrections).CorrectedBody);
        Assert.Equal("I corrected the wording.", Assert.Single(entry.Corrections).CorrectionReason);
        Assert.Equal("We will add the reviewer role.", Assert.Single(entry.Responses).Response);
        Assert.Equal(host.Lead.Id, Assert.Single(entry.Responses).RespondedBy.Id);
        Assert.True(entry.IsCurrent);
    }

    [Fact]
    public async Task ShouldMarkFeedbackHistoricalGivenSelectedSourceIsNoLongerCurrent()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, _, _, _) = await SeedAsync(host, twoEntries: false);
        var resolver = new RejectSelected();

        // Act
        var result = await Handler(host, resolver).HandleAsync(Context(new ListAdvisorFeedback(host.TenantId,
            host.ProgramId, handoffId)), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.False(Assert.Single(result.Value.Items).IsCurrent);
        Assert.Equal(First, Assert.Single(resolver.Selection));
    }

    [Fact]
    public async Task ShouldReturnTransientFailureGivenCurrentSourceCannotBeChecked()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, _, _, _) = await SeedAsync(host, twoEntries: false);

        // Act
        var result = await Handler(host, new Unavailable()).HandleAsync(Context(new ListAdvisorFeedback(host.TenantId,
            host.ProgramId, handoffId)), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.Error!.IsTransient);
    }

    [Fact]
    public async Task ShouldPaginateInAdmissionOrderGivenFeedbackCursor()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, feedbackId, secondId, _) = await SeedAsync(host, twoEntries: true);

        // Act
        var first = await Handler(host).HandleAsync(Context(new ListAdvisorFeedback(host.TenantId, host.ProgramId,
            handoffId, 1)), CancellationToken.None);
        var second = await Handler(host).HandleAsync(Context(new ListAdvisorFeedback(host.TenantId, host.ProgramId,
            handoffId, 1, first.Value.NextCursor)), CancellationToken.None);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.Equal(feedbackId, Assert.Single(first.Value.Items).AdvisorFeedbackId);
        Assert.Equal(feedbackId.ToString(), first.Value.NextCursor);
        Assert.True(second.IsSuccess, second.Error?.Message);
        Assert.Equal(secondId, Assert.Single(second.Value.Items).AdvisorFeedbackId);
        Assert.Null(second.Value.NextCursor);
    }

    [Fact]
    public async Task ShouldListOnlyMaterializedThreadsGivenAdmissionNotMaterializedYet()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, source) = await host.SeedReadyAsync();
        Assert.True((await host.RecordAsync(host.Feedback(handoffId, Uuid.CreateVersion4(), source, 2))).IsSuccess);

        // Act
        var before = await Handler(host).HandleAsync(Context(new ListAdvisorFeedback(host.TenantId, host.ProgramId,
            handoffId)), CancellationToken.None);
        await host.MaterializeAsync(handoffId);
        var after = await Handler(host).HandleAsync(Context(new ListAdvisorFeedback(host.TenantId, host.ProgramId,
            handoffId)), CancellationToken.None);

        // Assert
        Assert.Empty(before.Value.Items);
        Assert.Single(after.Value.Items);
    }

    [Fact]
    public async Task ShouldRejectInvalidCursorGivenFeedbackList()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, _, _, _) = await SeedAsync(host, twoEntries: false);

        // Act
        var result = await Handler(host).HandleAsync(Context(new ListAdvisorFeedback(host.TenantId, host.ProgramId,
            handoffId, Cursor: "not-a-uuid")), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldNotDiscloseFeedbackGivenDifferentProgramScope()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, _, _, _) = await SeedAsync(host, twoEntries: false);

        // Act
        var result = await Handler(host).HandleAsync(Context(new ListAdvisorFeedback(host.TenantId,
            Uuid.CreateVersion4(), handoffId)), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
    }

    sealed class AcceptCurrent : IAdvisorRecordVersionResolver
    {
        public ValueTask<Result> RequireCurrentAsync(Uuid tenantId, Uuid programId,
            IReadOnlyList<AdvisorRecordVersion> selection, CancellationToken ct = default) =>
            ValueTask.FromResult(Result.Success);
    }

    sealed class RejectSelected : IAdvisorRecordVersionResolver
    {
        public IReadOnlyList<AdvisorRecordVersion> Selection { get; private set; } = [];

        public ValueTask<Result> RequireCurrentAsync(Uuid tenantId, Uuid programId,
            IReadOnlyList<AdvisorRecordVersion> selection, CancellationToken ct = default)
        {
            Selection = selection.ToArray();
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The selected source has changed.")));
        }
    }

    sealed class Unavailable : IAdvisorRecordVersionResolver
    {
        public ValueTask<Result> RequireCurrentAsync(Uuid tenantId, Uuid programId,
            IReadOnlyList<AdvisorRecordVersion> selection, CancellationToken ct = default) =>
            ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The current source check is temporarily unavailable.", isTransient: true)));
    }
}
