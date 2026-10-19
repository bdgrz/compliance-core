using System.Security.Claims;
using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AdvisorCollaboration;

public sealed class ListAdvisorFeedbackHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldReturnAttributedFeedbackHistoryGivenScopedHandoff()
    {
        // Arrange
        await using var fixture = new Fixture();
        var handoffId = await fixture.SeedFeedbackAsync();
        var request = new ListAdvisorFeedback(fixture.TenantId, fixture.ProgramId, handoffId);

        // Act
        var result = await new ListAdvisorFeedbackHandler(fixture.Reader,
            new AcceptCurrentAdvisorRecordVersions()).HandleAsync(
            new RequestContext<ListAdvisorFeedback>(request, new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var entry = Assert.Single(result.Value.Items);
        Assert.Equal(fixture.FeedbackId, entry.AdvisorFeedbackId);
        Assert.Equal(fixture.RecordId, entry.RecordId);
        Assert.Equal(fixture.RecordVersionId, entry.RecordVersionId);
        Assert.Equal("change_request", entry.Kind);
        Assert.Equal("Clarify the evidence owner.", entry.Body);
        Assert.Equal(fixture.Advisor, entry.Author);
        Assert.Equal(Now.AddMinutes(2), entry.CreatedAt);
        Assert.Equal("Please explain who reviews the evidence.",
            Assert.Single(entry.Corrections).CorrectedBody);
        Assert.Equal("I corrected the wording.", Assert.Single(entry.Corrections).CorrectionReason);
        Assert.Equal("We will add the reviewer role.", Assert.Single(entry.Responses).Response);
        Assert.Equal(fixture.Lead, Assert.Single(entry.Responses).RespondedBy);
        Assert.True(entry.IsCurrent);
    }

    [Fact]
    public async Task ShouldMarkFeedbackHistoricalGivenSelectedSourceIsNoLongerCurrent()
    {
        // Arrange
        await using var fixture = new Fixture();
        var handoffId = await fixture.SeedFeedbackAsync();
        var resolver = new RejectSelectedAdvisorRecordVersion();

        // Act
        var result = await new ListAdvisorFeedbackHandler(fixture.Reader, resolver).HandleAsync(
            new RequestContext<ListAdvisorFeedback>(new ListAdvisorFeedback(fixture.TenantId,
                fixture.ProgramId, handoffId), new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var entry = Assert.Single(result.Value.Items);
        Assert.False(entry.IsCurrent);
        Assert.Equal(fixture.SelectedVersion,
            Assert.Single(resolver.Selection));
    }

    [Fact]
    public async Task ShouldReturnTransientFailureGivenCurrentSourceCannotBeChecked()
    {
        // Arrange
        await using var fixture = new Fixture();
        var handoffId = await fixture.SeedFeedbackAsync();
        var request = new ListAdvisorFeedback(fixture.TenantId, fixture.ProgramId, handoffId);

        // Act
        var result = await new ListAdvisorFeedbackHandler(fixture.Reader,
                new UnavailableAdvisorRecordVersionResolver())
            .HandleAsync(new RequestContext<ListAdvisorFeedback>(request, new ClaimsPrincipal()),
                CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.Error!.IsTransient);
    }

    [Fact]
    public async Task ShouldPaginateInAppendOrderGivenFeedbackCursor()
    {
        // Arrange
        await using var fixture = new Fixture();
        var handoffId = await fixture.SeedTwoFeedbackEntriesAsync();

        // Act
        var first = await new ListAdvisorFeedbackHandler(fixture.Reader,
            new AcceptCurrentAdvisorRecordVersions()).HandleAsync(
            new RequestContext<ListAdvisorFeedback>(new ListAdvisorFeedback(fixture.TenantId,
                fixture.ProgramId, handoffId, 1), new ClaimsPrincipal()), CancellationToken.None);
        var second = await new ListAdvisorFeedbackHandler(fixture.Reader,
            new AcceptCurrentAdvisorRecordVersions()).HandleAsync(
            new RequestContext<ListAdvisorFeedback>(new ListAdvisorFeedback(fixture.TenantId,
                fixture.ProgramId, handoffId, 1, first.Value.NextCursor), new ClaimsPrincipal()),
            CancellationToken.None);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.Equal(fixture.FeedbackId, Assert.Single(first.Value.Items).AdvisorFeedbackId);
        Assert.Equal(fixture.FeedbackId.ToString(), first.Value.NextCursor);
        Assert.True(second.IsSuccess, second.Error?.Message);
        Assert.Equal(fixture.SecondFeedbackId, Assert.Single(second.Value.Items).AdvisorFeedbackId);
        Assert.Null(second.Value.NextCursor);
    }

    [Fact]
    public async Task ShouldRejectInvalidCursorGivenFeedbackList()
    {
        // Arrange
        await using var fixture = new Fixture();
        var handoffId = await fixture.SeedFeedbackAsync();

        // Act
        var result = await new ListAdvisorFeedbackHandler(fixture.Reader,
            new AcceptCurrentAdvisorRecordVersions()).HandleAsync(
            new RequestContext<ListAdvisorFeedback>(new ListAdvisorFeedback(fixture.TenantId,
                fixture.ProgramId, handoffId, Cursor: "not-a-uuid"), new ClaimsPrincipal()),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldNotDiscloseFeedbackGivenDifferentProgramScope()
    {
        // Arrange
        await using var fixture = new Fixture();
        var handoffId = await fixture.SeedFeedbackAsync();

        // Act
        var result = await new ListAdvisorFeedbackHandler(fixture.Reader,
            new AcceptCurrentAdvisorRecordVersions()).HandleAsync(
            new RequestContext<ListAdvisorFeedback>(new ListAdvisorFeedback(fixture.TenantId,
                Uuid.CreateVersion4(), handoffId), new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
    }

    sealed class AcceptCurrentAdvisorRecordVersions : IAdvisorRecordVersionResolver
    {
        public ValueTask<Result> RequireCurrentAsync(Uuid tenantId, Uuid programId,
            IReadOnlyList<AdvisorRecordVersion> selection, CancellationToken ct = default) =>
            ValueTask.FromResult(Result.Success);
    }

    sealed class RejectSelectedAdvisorRecordVersion : IAdvisorRecordVersionResolver
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

    sealed class UnavailableAdvisorRecordVersionResolver : IAdvisorRecordVersionResolver
    {
        public ValueTask<Result> RequireCurrentAsync(Uuid tenantId, Uuid programId,
            IReadOnlyList<AdvisorRecordVersion> selection, CancellationToken ct = default) =>
            ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The current source check is temporarily unavailable.", isTransient: true)));
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public Uuid RecordId { get; } = Uuid.CreateVersion4();
        public Uuid RecordVersionId { get; } = Uuid.CreateVersion4();
        public AdvisorRecordVersion SelectedVersion =>
            AdvisorRecordVersion.ControlVersion(RecordId, RecordVersionId, 1);
        public Uuid SecondRecordId { get; } = Uuid.CreateVersion4();
        public Uuid SecondRecordVersionId { get; } = Uuid.CreateVersion4();
        public Uuid FeedbackId { get; } = Uuid.CreateVersion4();
        public Uuid SecondFeedbackId { get; } = Uuid.CreateVersion4();
        public ActorReference Advisor { get; }
        public ActorReference Lead { get; }
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }

        public Fixture()
        {
            Advisor = ActorReference.ForMember(Uuid.CreateVersion4(), "Assigned Advisor");
            Lead = ActorReference.ForMember(RbacIds.Member(TenantId, UserId), "Compliance lead");
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
        }

        public Task<Uuid> SeedFeedbackAsync() => SeedAsync(twoEntries: false);

        public Task<Uuid> SeedTwoFeedbackEntriesAsync() => SeedAsync(twoEntries: true);

        async Task<Uuid> SeedAsync(bool twoEntries)
        {
            var handoffId = Uuid.CreateVersion4();
            var handoff = new AdvisorHandoff(TenantId, ProgramId, handoffId);
            Assert.True(handoff.Create("Type I readiness review",
                [SelectedVersion,
                    AdvisorRecordVersion.ControlVersion(SecondRecordId, SecondRecordVersionId, 1)],
                new DateOnly(2026, 12, 31), Lead, Now).IsSuccess);
            Assert.True(handoff.MarkReadyForReview(1, Lead, Now.AddMinutes(1)).IsSuccess);
            Assert.True(handoff.RecordFeedback(FeedbackId, SelectedVersion,
                "change_request", "Clarify the evidence owner.", Advisor, Now.AddMinutes(2)).IsSuccess);
            if (twoEntries)
            {
                Assert.True(handoff.RecordFeedback(SecondFeedbackId,
                    AdvisorRecordVersion.ControlVersion(SecondRecordId, SecondRecordVersionId, 1),
                    "comment", "Add the reviewer role.", Advisor,
                    Now.AddMinutes(3)).IsSuccess);
            }
            else
            {
                Assert.True(handoff.CorrectFeedback(FeedbackId, Uuid.CreateVersion4(),
                    "Please explain who reviews the evidence.", "I corrected the wording.",
                    Advisor, Now.AddMinutes(3)).IsSuccess);
                Assert.True(handoff.RespondToFeedback(FeedbackId,
                    "We will add the reviewer role.", Lead, Now.AddMinutes(4)).IsSuccess);
            }
            await Writer.SaveAsync(handoff, new RequestDispatchContext(RequestActor.System));
            return handoffId;
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }
}
