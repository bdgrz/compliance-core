using System.Security.Claims;
using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AdvisorCollaboration;

/// <summary>The handoff owner's commands: exact retries, changed intent, revision guards and actor scope.</summary>
public sealed class AdvisorHandoffCommandHandlerTests
{
    [Fact]
    public async Task ShouldCreateDraftHandoffGivenPersonalHttpActor()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var handoffId = Uuid.CreateVersion4();
        var source = AdvisorHost.NewSource();

        // Act
        var result = await host.CreateHandler.HandleAsync(host.Http(new CreateAdvisorHandoff(host.TenantId,
            host.ProgramId, handoffId, [source], "  Type I controls  ", AdvisorHost.Expiry)), CancellationToken.None);
        var saved = await host.SnapshotAsync(handoffId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(saved.IsCreated);
        Assert.Equal("draft", saved.Status);
        Assert.Equal("Type I controls", saved.Purpose);
        Assert.Equal(AdvisorHost.Expiry, saved.ExpiresOn);
        Assert.Equal(1, saved.Revision);
        Assert.Equal(1, saved.Generation);
        Assert.Equal(AdvisorHost.Start, saved.CreatedAt);
        Assert.Equal(host.Lead.Id, saved.CreatedBy!.Id);
        Assert.Equal([source], saved.Selection);
    }

    [Fact]
    public async Task ShouldKeepCompleteSelectionAsVerifiedContentGivenManySources()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var handoffId = Uuid.CreateVersion4();
        var selection = Enumerable.Range(0, AdvisorHandoff.MaximumSelectionCount)
            .Select(_ => AdvisorHost.NewSource()).ToArray();

        // Act
        var result = await host.CreateAsync(handoffId, selection);
        var saved = await host.HandoffAsync(handoffId);
        var read = await host.SnapshotAsync(handoffId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.NotNull(saved.SelectionManifest);
        Assert.Equal(selection, read.Selection);
    }

    [Fact]
    public async Task ShouldReturnOriginalResultGivenExactCreateRetryAfterLaterOwnerChange()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var handoffId = Uuid.CreateVersion4();
        var source = AdvisorHost.NewSource();
        Assert.True((await host.CreateAsync(handoffId, [source])).IsSuccess);
        Assert.True((await host.ReadyAsync(handoffId, 1)).IsSuccess);
        Assert.True((await host.EndAsync(handoffId, 2, revoke: true)).IsSuccess);

        // Act
        var retry = await host.CreateAsync(handoffId, [source]);
        var saved = await host.HandoffAsync(handoffId);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal("revoked", saved.Status);
        Assert.Equal(3, saved.Revision);
        Assert.Single(await host.EventsAsync<AdvisorHandoffCreated>(saved.Stream));
    }

    [Fact]
    public async Task ShouldConflictGivenSameHandoffIdentityWithChangedCreateIntent()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var handoffId = Uuid.CreateVersion4();
        var source = AdvisorHost.NewSource();
        Assert.True((await host.CreateAsync(handoffId, [source])).IsSuccess);

        // Act
        var changedPurpose = await host.CreateAsync(handoffId, [source], "A different purpose");
        var changedSelection = await host.CreateAsync(handoffId, [AdvisorHost.NewSource()]);
        var saved = await host.SnapshotAsync(handoffId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, changedPurpose.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changedSelection.Error?.Kind);
        Assert.Equal("Type I controls", saved.Purpose);
        Assert.Equal([source], saved.Selection);
        Assert.Equal(1, saved.Revision);
    }

    [Fact]
    public async Task ShouldRejectWithoutAppendGivenMissingRouteIdentity()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var source = AdvisorHost.NewSource();
        var (handoffId, _) = await host.SeedReadyAsync();

        // Act
        var create = await host.CreateAsync(Uuid.Empty, [source]);
        var revise = await host.ReviseHandler.HandleAsync(host.Http(new ReviseAdvisorHandoffSelection(Uuid.Empty,
            host.TenantId, host.ProgramId, handoffId, 2, [source], "Reason")), CancellationToken.None);
        var ready = await host.ReadyHandler.HandleAsync(host.Http(new MarkAdvisorHandoffReadyForReview(Uuid.Empty,
            host.TenantId, host.ProgramId, handoffId, 2)), CancellationToken.None);
        var close = await host.CloseHandler.HandleAsync(host.Http(new CloseAdvisorHandoff(Uuid.Empty, host.TenantId,
            host.ProgramId, handoffId, "Done", 2)), CancellationToken.None);
        var revoke = await host.RevokeHandler.HandleAsync(host.Http(new RevokeAdvisorHandoff(Uuid.CreateVersion4(),
            host.TenantId, Uuid.Empty, handoffId, "Done", 2)), CancellationToken.None);
        var negative = await host.EndAsync(handoffId, -1, revoke: false);
        var saved = await host.HandoffAsync(handoffId);

        // Assert
        Assert.All(new[] { create, revise, ready, close, revoke, negative },
            result => Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind));
        Assert.Equal(2, saved.Revision);
    }

    [Fact]
    public async Task ShouldRejectPreviouslySharedSourceGivenRepeatedHandoff()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (previousId, source) = await host.SeedReadyAsync();
        Assert.True((await host.EndAsync(previousId, 2, revoke: false)).IsSuccess);
        var handoffId = Uuid.CreateVersion4();

        // Act
        var result = await host.CreateAsync(handoffId, [source], "Follow-up review", previousId);
        var saved = await host.HandoffAsync(handoffId);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.False(saved.IsCreated);
    }

    [Fact]
    public async Task ShouldCreateChangedSourceGivenRepeatedHandoff()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (previousId, source) = await host.SeedReadyAsync();
        Assert.True((await host.EndAsync(previousId, 2, revoke: false)).IsSuccess);
        var handoffId = Uuid.CreateVersion4();
        var changed = new AdvisorRecordVersion(source.RecordId, Uuid.CreateVersion4());

        // Act
        var result = await host.CreateAsync(handoffId, [changed], "Follow-up review", previousId);
        var saved = await host.SnapshotAsync(handoffId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.NotEqual(source.VersionId, Assert.Single(saved.Selection).VersionId);
        Assert.Equal(previousId, saved.ComparedToHandoffId);
    }

    [Fact]
    public async Task ShouldRejectComparisonAgainstAnUnfinishedHandoffGivenRepeatSelection()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (previousId, source) = await host.SeedReadyAsync();
        var changed = new AdvisorRecordVersion(source.RecordId, Uuid.CreateVersion4());

        // Act
        var result = await host.CreateAsync(Uuid.CreateVersion4(), [changed], "Follow-up review", previousId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldRejectGivenExpiredHandoffAndKeepNothingGivenInvalidCreate()
    {
        // Arrange
        await using var host = new AdvisorHost(new DateTimeOffset(2027, 1, 2, 0, 0, 0, TimeSpan.Zero));
        var handoffId = Uuid.CreateVersion4();

        // Act
        var expired = await host.CreateAsync(handoffId, [AdvisorHost.NewSource()]);
        var blankPurpose = await host.CreateAsync(handoffId, [AdvisorHost.NewSource()], "  ");
        var emptySelection = await host.CreateAsync(handoffId, []);
        var saved = await host.HandoffAsync(handoffId);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, expired.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, blankPurpose.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, emptySelection.Error?.Kind);
        Assert.False(saved.IsCreated);
    }

    [Fact]
    public async Task ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome()
    {
        // Arrange
        await using var host = new AdvisorHost(wrapExecutor: inner => new AdvisorHost.CommitThenFailOnceExecutor(inner));
        var handoffId = Uuid.CreateVersion4();

        // Act
        var result = await host.CreateAsync(handoffId, [AdvisorHost.NewSource()]);
        var saved = await host.HandoffAsync(handoffId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Single(await host.EventsAsync<AdvisorHandoffCreated>(saved.Stream));
        Assert.Equal(1, saved.Revision);
    }

    [Fact]
    public async Task ShouldNotCreateHandoffGivenSystemDispatch()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var handoffId = Uuid.CreateVersion4();
        var request = new CreateAdvisorHandoff(host.TenantId, host.ProgramId, handoffId,
            [AdvisorHost.NewSource()], "Review", AdvisorHost.Expiry);

        // Act
        var result = await host.CreateHandler.HandleAsync(AdvisorHost.System(request), CancellationToken.None);
        var saved = await host.HandoffAsync(handoffId);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.False(saved.IsCreated);
    }

    [Fact]
    public async Task ShouldReviseSelectionGivenCurrentHandoffRevision()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, _) = await host.SeedReadyAsync();
        var revised = AdvisorHost.NewSource();
        host.Clock.Advance(TimeSpan.FromMinutes(5));

        // Act
        var result = await host.ReviseAsync(handoffId, 2, [revised], "Updated the review scope");
        var saved = await host.SnapshotAsync(handoffId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("draft", saved.Status);
        Assert.Equal(3, saved.Revision);
        Assert.Equal(2, saved.Generation);
        Assert.Equal(AdvisorHost.Start.AddMinutes(5), saved.UpdatedAt);
        Assert.Equal([revised], saved.Selection);
    }

    [Fact]
    public async Task ShouldNotReviseSelectionGivenStaleHandoffRevision()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, source) = await host.SeedReadyAsync();

        // Act
        var result = await host.ReviseAsync(handoffId, 1, [AdvisorHost.NewSource()], "Changed selection");
        var saved = await host.SnapshotAsync(handoffId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(2, saved.Revision);
        Assert.Equal([source], saved.Selection);
    }

    [Fact]
    public async Task ShouldReturnOriginalResultGivenExactReviseRetryAfterLaterOwnerChange()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, _) = await host.SeedReadyAsync();
        var revised = AdvisorHost.NewSource();
        var commandId = Uuid.CreateVersion4();
        Assert.True((await host.ReviseAsync(handoffId, 2, [revised], "Updated scope", commandId)).IsSuccess);
        Assert.True((await host.ReadyAsync(handoffId, 3)).IsSuccess);

        // Act
        var retry = await host.ReviseAsync(handoffId, 2, [revised], "Updated scope", commandId);
        var saved = await host.HandoffAsync(handoffId);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(4, saved.Revision);
        Assert.Single(await host.EventsAsync<AdvisorHandoffSelectionRevised>(saved.Stream));
    }

    [Fact]
    public async Task ShouldConflictGivenSameCommandIdentityWithChangedIntentOrOriginalRevision()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, _) = await host.SeedReadyAsync();
        var revised = AdvisorHost.NewSource();
        var commandId = Uuid.CreateVersion4();
        Assert.True((await host.ReviseAsync(handoffId, 2, [revised], "Updated scope", commandId)).IsSuccess);

        // Act
        var otherSelection = await host.ReviseAsync(handoffId, 2, [AdvisorHost.NewSource()], "Updated scope", commandId);
        var otherReason = await host.ReviseAsync(handoffId, 2, [revised], "Another reason", commandId);
        var otherRevision = await host.ReviseAsync(handoffId, 3, [revised], "Updated scope", commandId);
        var otherKind = await host.EndAsync(handoffId, 2, revoke: true, commandId);
        var saved = await host.HandoffAsync(handoffId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, otherSelection.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, otherReason.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, otherRevision.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, otherKind.Error?.Kind);
        Assert.Equal(3, saved.Revision);
        Assert.Equal("draft", saved.Status);
    }

    [Fact]
    public async Task ShouldRejectUnchangedSelectionGivenRevision()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, source) = await host.SeedReadyAsync();

        // Act
        var result = await host.ReviseAsync(handoffId, 2, [source]);
        var saved = await host.HandoffAsync(handoffId);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(2, saved.Revision);
    }

    [Fact]
    public async Task ShouldRejectPreviouslySharedSourceGivenRepeatedHandoffSelectionRevision()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (previousId, source) = await host.SeedReadyAsync();
        Assert.True((await host.EndAsync(previousId, 2, revoke: false)).IsSuccess);
        var handoffId = Uuid.CreateVersion4();
        Assert.True((await host.CreateAsync(handoffId, [new AdvisorRecordVersion(source.RecordId, Uuid.CreateVersion4())],
            "Follow-up review", previousId)).IsSuccess);

        // Act
        var result = await host.ReviseAsync(handoffId, 1, [source], "Restoring the prior version");
        var saved = await host.SnapshotAsync(handoffId);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(1, saved.Revision);
        Assert.NotEqual(source.VersionId, Assert.Single(saved.Selection).VersionId);
    }

    [Fact]
    public async Task ShouldMarkDraftHandoffReadyGivenCurrentRevision()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var handoffId = Uuid.CreateVersion4();
        Assert.True((await host.CreateAsync(handoffId, [AdvisorHost.NewSource()])).IsSuccess);
        host.Clock.Advance(TimeSpan.FromMinutes(1));

        // Act
        var result = await host.ReadyAsync(handoffId, 1);
        var saved = await host.SnapshotAsync(handoffId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("ready_for_review", saved.Status);
        Assert.Equal(2, saved.Revision);
        Assert.True(saved.WasReadyForReview);
        Assert.Equal(AdvisorHost.Start.AddMinutes(1), saved.UpdatedAt);
    }

    [Fact]
    public async Task ShouldKeepDraftHandoffClosedGivenAStaleSelectedSource()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var handoffId = Uuid.CreateVersion4();
        Assert.True((await host.CreateAsync(handoffId, [AdvisorHost.NewSource()])).IsSuccess);
        host.Resolver.Reject = RequestErrorKind.Conflict;

        // Act
        var result = await host.ReadyAsync(handoffId, 1);
        var saved = await host.SnapshotAsync(handoffId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal("draft", saved.Status);
        Assert.Equal(1, saved.Revision);
    }

    [Fact]
    public async Task ShouldNotMarkReadyGivenStaleRevisionOrAlreadyReadyHandoff()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, _) = await host.SeedReadyAsync();

        // Act
        var stale = await host.ReadyAsync(handoffId, 1);
        var again = await host.ReadyAsync(handoffId, 2);
        var saved = await host.HandoffAsync(handoffId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, again.Error?.Kind);
        Assert.Equal(2, saved.Revision);
    }

    [Fact]
    public async Task ShouldReturnOriginalResultGivenExactReadyRetryAfterLaterOwnerChange()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var handoffId = Uuid.CreateVersion4();
        var commandId = Uuid.CreateVersion4();
        Assert.True((await host.CreateAsync(handoffId, [AdvisorHost.NewSource()])).IsSuccess);
        Assert.True((await host.ReadyAsync(handoffId, 1, commandId)).IsSuccess);
        Assert.True((await host.EndAsync(handoffId, 2, revoke: true)).IsSuccess);

        // Act
        var retry = await host.ReadyAsync(handoffId, 1, commandId);
        var saved = await host.HandoffAsync(handoffId);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal("revoked", saved.Status);
        Assert.Single(await host.EventsAsync<AdvisorHandoffReadiedForReview>(saved.Stream));
    }

    [Fact]
    public async Task ShouldNotMarkHandoffReadyGivenSystemDispatch()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var handoffId = Uuid.CreateVersion4();
        Assert.True((await host.CreateAsync(handoffId, [AdvisorHost.NewSource()])).IsSuccess);

        // Act
        var result = await host.ReadyHandler.HandleAsync(AdvisorHost.System(new MarkAdvisorHandoffReadyForReview(
            Uuid.CreateVersion4(), host.TenantId, host.ProgramId, handoffId, 1)), CancellationToken.None);
        var saved = await host.HandoffAsync(handoffId);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal("draft", saved.Status);
        Assert.Equal(1, saved.Revision);
    }

    [Theory]
    [InlineData(false, "closed")]
    [InlineData(true, "revoked")]
    public async Task ShouldEndOpenHandoffGivenAuthorizedManagementCommand(bool revoke, string status)
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, _) = await host.SeedReadyAsync();

        // Act
        var result = await host.EndAsync(handoffId, 2, revoke);
        var saved = await host.HandoffAsync(handoffId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(status, saved.Status);
        Assert.Equal(3, saved.Revision);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReturnOriginalResultGivenExactEndRetryAndConflictGivenChangedIntent(bool revoke)
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, _) = await host.SeedReadyAsync();
        var commandId = Uuid.CreateVersion4();
        Assert.True((await host.EndAsync(handoffId, 2, revoke, commandId)).IsSuccess);

        // Act
        var retry = await host.EndAsync(handoffId, 2, revoke, commandId);
        var otherRevision = await host.EndAsync(handoffId, 1, revoke, commandId);
        var otherKind = await host.EndAsync(handoffId, 2, !revoke, commandId);
        var secondEnd = await host.EndAsync(handoffId, 3, revoke);
        var saved = await host.HandoffAsync(handoffId);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, otherRevision.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, otherKind.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, secondEnd.Error?.Kind);
        Assert.Equal(3, saved.Revision);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldNotEndHandoffGivenStaleRevisionOrBlankRationale(bool revoke)
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, _) = await host.SeedReadyAsync();

        // Act
        var stale = await host.EndAsync(handoffId, 1, revoke);
        var blank = revoke
            ? await host.RevokeHandler.HandleAsync(host.Http(new RevokeAdvisorHandoff(Uuid.CreateVersion4(),
                host.TenantId, host.ProgramId, handoffId, " ", 2)), CancellationToken.None)
            : await host.CloseHandler.HandleAsync(host.Http(new CloseAdvisorHandoff(Uuid.CreateVersion4(),
                host.TenantId, host.ProgramId, handoffId, " ", 2)), CancellationToken.None);
        var saved = await host.HandoffAsync(handoffId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, blank.Error?.Kind);
        Assert.Equal("ready_for_review", saved.Status);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("revise")]
    [InlineData("ready")]
    [InlineData("close")]
    [InlineData("revoke")]
    public async Task ShouldNotMutateHandoffGivenDifferentProgramScope(string operation)
    {
        // Arrange
        await using var host = new AdvisorHost();
        var otherProgramId = Uuid.CreateVersion4();
        var (handoffId, _) = await host.SeedReadyAsync();
        var source = AdvisorHost.NewSource();

        // Act
        var result = operation switch
        {
            "create" => await host.CreateHandler.HandleAsync(host.Http(new CreateAdvisorHandoff(host.TenantId,
                otherProgramId, handoffId, [source], "Review", AdvisorHost.Expiry)), CancellationToken.None),
            "revise" => await host.ReviseHandler.HandleAsync(host.Http(new ReviseAdvisorHandoffSelection(
                Uuid.CreateVersion4(), host.TenantId, otherProgramId, handoffId, 2, [source], "Wrong program")),
                CancellationToken.None),
            "ready" => await host.ReadyHandler.HandleAsync(host.Http(new MarkAdvisorHandoffReadyForReview(
                Uuid.CreateVersion4(), host.TenantId, otherProgramId, handoffId, 2)), CancellationToken.None),
            "close" => await host.CloseHandler.HandleAsync(host.Http(new CloseAdvisorHandoff(Uuid.CreateVersion4(),
                host.TenantId, otherProgramId, handoffId, "Done", 2)), CancellationToken.None),
            "revoke" => await host.RevokeHandler.HandleAsync(host.Http(new RevokeAdvisorHandoff(Uuid.CreateVersion4(),
                host.TenantId, otherProgramId, handoffId, "Done", 2)), CancellationToken.None),
            _ => throw new InvalidOperationException(),
        };
        var saved = await host.HandoffAsync(handoffId);

        // Assert
        Assert.Equal(operation == "create" ? RequestErrorKind.Conflict : RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Equal("ready_for_review", saved.Status);
        Assert.Equal(2, saved.Revision);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("revise")]
    [InlineData("ready")]
    [InlineData("close")]
    [InlineData("revoke")]
    public async Task ShouldRejectMcpInvocationGivenPersonalHandoffMutation(string operation)
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, _) = await host.SeedReadyAsync();
        var source = AdvisorHost.NewSource();
        var dispatch = new RequestDispatchContext(AdvisorHost.Personal(host.UserId),
            new McpInvocation("advisor-handoff-mutation"));
        var commandId = Uuid.CreateVersion4();

        // Act
        var result = operation switch
        {
            "create" => await host.CreateHandler.HandleAsync(new Cntryl.Portia.RequestContext<CreateAdvisorHandoff>(
                new CreateAdvisorHandoff(host.TenantId, host.ProgramId, Uuid.CreateVersion4(), [source], "Review",
                    AdvisorHost.Expiry), dispatch), CancellationToken.None),
            "revise" => await host.ReviseHandler.HandleAsync(new Cntryl.Portia.RequestContext<ReviseAdvisorHandoffSelection>(
                new ReviseAdvisorHandoffSelection(commandId, host.TenantId, host.ProgramId, handoffId, 2, [source],
                    "Updated scope"), dispatch), CancellationToken.None),
            "ready" => await host.ReadyHandler.HandleAsync(new Cntryl.Portia.RequestContext<MarkAdvisorHandoffReadyForReview>(
                new MarkAdvisorHandoffReadyForReview(commandId, host.TenantId, host.ProgramId, handoffId, 2),
                dispatch), CancellationToken.None),
            "close" => await host.CloseHandler.HandleAsync(new Cntryl.Portia.RequestContext<CloseAdvisorHandoff>(
                new CloseAdvisorHandoff(commandId, host.TenantId, host.ProgramId, handoffId, "Done", 2), dispatch),
                CancellationToken.None),
            "revoke" => await host.RevokeHandler.HandleAsync(new Cntryl.Portia.RequestContext<RevokeAdvisorHandoff>(
                new RevokeAdvisorHandoff(commandId, host.TenantId, host.ProgramId, handoffId, "Done", 2), dispatch),
                CancellationToken.None),
            _ => throw new InvalidOperationException(),
        };
        var saved = await host.HandoffAsync(handoffId);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal("ready_for_review", saved.Status);
        Assert.Equal(2, saved.Revision);
    }

    [Theory]
    [InlineData("close")]
    [InlineData("revoke")]
    public async Task ShouldRejectSystemDispatchGivenHandoffTermination(string operation)
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, _) = await host.SeedReadyAsync();
        var commandId = Uuid.CreateVersion4();

        // Act
        var result = operation == "close"
            ? await host.CloseHandler.HandleAsync(AdvisorHost.System(new CloseAdvisorHandoff(commandId, host.TenantId,
                host.ProgramId, handoffId, "Done", 2)), CancellationToken.None)
            : await host.RevokeHandler.HandleAsync(AdvisorHost.System(new RevokeAdvisorHandoff(commandId, host.TenantId,
                host.ProgramId, handoffId, "Done", 2)), CancellationToken.None);
        var saved = await host.HandoffAsync(handoffId);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal("ready_for_review", saved.Status);
    }
}
