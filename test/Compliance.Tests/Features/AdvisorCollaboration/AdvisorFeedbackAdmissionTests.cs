using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Authorizers;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Events;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Queries;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Reactors;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AdvisorCollaboration;

/// <summary>
///     Feedback is admitted on the handoff and materialized independently: the admission shares the handoff stream
///     with revocation and closure, an exact retry replays the original acknowledgment, and what was admitted is
///     materialized from the frozen intent even after the handoff ended.
/// </summary>
public sealed class AdvisorFeedbackAdmissionTests
{
    [Fact]
    public async Task ShouldRetrySameFeedbackGivenCrashAfterContentPreparationBeforeAdmissionAppend()
    {
        // Arrange
        await using var host = new AdvisorHost(wrapExecutor: inner => new AdvisorHost.InterleaveExecutor(inner, 3,
            () => throw new InvalidOperationException("Crash before admission append.")));
        var (handoffId, source) = await host.SeedReadyAsync();
        var feedbackId = Uuid.CreateVersion4();
        var request = host.Feedback(handoffId, feedbackId, source, 2);
        await Assert.ThrowsAsync<InvalidOperationException>(() => host.RecordAsync(request));
        Assert.Empty((await host.HandoffAsync(handoffId)).Admitted);
        host.Clock.Advance(TimeSpan.FromMinutes(5));

        // Act
        var retry = await host.RecordAsync(request);
        var processes = await host.MaterializeAsync(handoffId);
        var thread = await host.ThreadAsync(handoffId, feedbackId);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(ProcessState.Completed, Assert.Single(processes).State);
        Assert.Single((await host.HandoffAsync(handoffId)).Admitted);
        Assert.Equal(AdvisorHost.Start.AddMinutes(5), thread.Recorded!.CreatedAt);
        Assert.Equal(request.Body, thread.Recorded.Body);
    }

    [Fact]
    public async Task ShouldAdmitFeedbackAndAcknowledgeGivenReadyHandoffAndSelectedExactVersion()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, source) = await host.SeedReadyAsync();
        var feedbackId = Uuid.CreateVersion4();
        var advisorUserId = Uuid.CreateVersion4();

        // Act
        var result = await host.RecordAsync(host.Feedback(handoffId, feedbackId, source, 2), advisorUserId);
        var saved = await host.HandoffAsync(handoffId);
        var thread = await host.ThreadAsync(handoffId, feedbackId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(feedbackId, result.Value.OperationId);
        Assert.Equal(3, saved.Revision);
        var admitted = Assert.Single(saved.Admitted);
        Assert.Equal(feedbackId, admitted.Operation.LogicalId);
        Assert.Equal(saved.Generation, admitted.Generation);
        Assert.Equal(RbacIds.Member(host.TenantId, advisorUserId).ToString(), admitted.Operation.Actor.Id);
        Assert.False(thread.IsMaterialized);
    }

    [Fact]
    public async Task ShouldMaterializeThreadWithOriginalActorAndAdmissionInstantGivenCommittedAdmission()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, source) = await host.SeedReadyAsync();
        var feedbackId = Uuid.CreateVersion4();
        var advisorUserId = Uuid.CreateVersion4();
        Assert.True((await host.RecordAsync(host.Feedback(handoffId, feedbackId, source, 2, "  Body  "),
            advisorUserId)).IsSuccess);
        host.Clock.Advance(TimeSpan.FromHours(3));

        // Act
        var records = await host.MaterializeAsync(handoffId);
        var thread = await host.ThreadAsync(handoffId, feedbackId);

        // Assert
        Assert.Equal(ProcessState.Completed, Assert.Single(records).State);
        Assert.Equal(1, thread.Revision);
        var recorded = thread.Recorded!;
        Assert.Equal("Body", recorded.Body);
        Assert.Equal("comment", recorded.Kind);
        Assert.Equal(RbacIds.Member(host.TenantId, advisorUserId).ToString(), recorded.Author.Id);
        Assert.Equal(AdvisorHost.Start, recorded.CreatedAt);
        Assert.Equal(source.RecordId, recorded.RecordId);
        Assert.Equal(source.VersionId, recorded.RecordVersionId);
    }

    [Fact]
    public async Task ShouldRecordFeedbackAgainstNativePolicyVersionGivenSelectedSource()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var policy = AdvisorRecordVersion.PolicyVersion(Uuid.CreateVersion4(), 3, 7);
        var (handoffId, _) = await host.SeedReadyAsync(policy);
        var feedbackId = Uuid.CreateVersion4();

        // Act
        var result = await host.RecordAsync(host.Feedback(handoffId, feedbackId, policy, 2, "Needs an owner.",
            "change_request"));
        await host.MaterializeAsync(handoffId);
        var thread = await host.ThreadAsync(handoffId, feedbackId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(policy, thread.Recorded!.SelectedSource);
        Assert.Equal("change_request", thread.Recorded.Kind);
    }

    [Fact]
    public async Task ShouldReplayOriginalAcknowledgmentGivenExactRetryAfterHandoffRevocation()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, source) = await host.SeedReadyAsync();
        var feedbackId = Uuid.CreateVersion4();
        var request = host.Feedback(handoffId, feedbackId, source, 2);
        var first = await host.RecordAsync(request);
        Assert.True((await host.EndAsync(handoffId, 3, revoke: true)).IsSuccess);

        // Act
        var retry = await host.RecordAsync(request);
        var saved = await host.HandoffAsync(handoffId);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(first.Value, retry.Value);
        Assert.Equal("revoked", saved.Status);
        Assert.Single(saved.Admitted);
        Assert.Equal(4, saved.Revision);
    }

    [Fact]
    public async Task ShouldConflictGivenSameFeedbackIdentityWithChangedIntent()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, source) = await host.SeedReadyAsync();
        var feedbackId = Uuid.CreateVersion4();
        Assert.True((await host.RecordAsync(host.Feedback(handoffId, feedbackId, source, 2))).IsSuccess);

        // Act
        var otherBody = await host.RecordAsync(host.Feedback(handoffId, feedbackId, source, 2, "Another body."));
        var otherKind = await host.RecordAsync(host.Feedback(handoffId, feedbackId, source, 2,
            "Please clarify the control owner.", "change_request"));
        var otherRevision = await host.RecordAsync(host.Feedback(handoffId, feedbackId, source, 3));
        await host.MaterializeAsync(handoffId);
        var thread = await host.ThreadAsync(handoffId, feedbackId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, otherBody.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, otherKind.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, otherRevision.Error?.Kind);
        Assert.Equal("Please clarify the control owner.", thread.Recorded!.Body);
        Assert.Equal(3, (await host.HandoffAsync(handoffId)).Revision);
    }

    [Fact]
    public async Task ShouldRefuseAdmissionGivenHandoffRevokedFirst()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, source) = await host.SeedReadyAsync();
        Assert.True((await host.EndAsync(handoffId, 2, revoke: true)).IsSuccess);

        // Act
        var stale = await host.RecordAsync(host.Feedback(handoffId, Uuid.CreateVersion4(), source, 2));
        var current = await host.RecordAsync(host.Feedback(handoffId, Uuid.CreateVersion4(), source, 3));
        var saved = await host.HandoffAsync(handoffId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, current.Error?.Kind);
        Assert.Empty(saved.Admitted);
        Assert.Equal(3, saved.Revision);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRefuseEndGivenFeedbackAdmittedFirstAndStillMaterializeIt(bool revoke)
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, source) = await host.SeedReadyAsync();
        var feedbackId = Uuid.CreateVersion4();
        Assert.True((await host.RecordAsync(host.Feedback(handoffId, feedbackId, source, 2))).IsSuccess);

        // Act
        var staleEnd = await host.EndAsync(handoffId, 2, revoke);
        var freshEnd = await host.EndAsync(handoffId, 3, revoke);
        await host.MaterializeAsync(handoffId);
        var saved = await host.HandoffAsync(handoffId);
        var thread = await host.ThreadAsync(handoffId, feedbackId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, staleEnd.Error?.Kind);
        Assert.True(freshEnd.IsSuccess, freshEnd.Error?.Message);
        Assert.Equal(revoke ? "revoked" : "closed", saved.Status);
        Assert.True(thread.IsMaterialized);
        Assert.Equal("Please clarify the control owner.", thread.Recorded!.Body);
    }

    [Fact]
    public async Task ShouldRefuseAdmissionGivenRevocationCommitsAfterAdmissionWasPrepared()
    {
        // Arrange
        AdvisorHost? host = null;
        Func<Task> interfere = () => Task.CompletedTask;
        host = new AdvisorHost(wrapExecutor: inner => new AdvisorHost.InterleaveExecutor(inner, 3, () => interfere()));
        await using var _ = host;
        var handoffId = Uuid.CreateVersion4();
        var source = AdvisorHost.NewSource();
        Assert.True((await host.CreateAsync(handoffId, [source])).IsSuccess);
        Assert.True((await host.ReadyAsync(handoffId, 1)).IsSuccess);
        interfere = async () => Assert.True((await host.EndAsync(handoffId, 2, revoke: true)).IsSuccess);

        // Act
        var result = await host.RecordAsync(host.Feedback(handoffId, Uuid.CreateVersion4(), source, 2));
        var saved = await host.HandoffAsync(handoffId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal("revoked", saved.Status);
        Assert.Empty(saved.Admitted);
        Assert.Equal(3, saved.Revision);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRefuseEndGivenFeedbackCommitsAfterTheEndWasPrepared(bool revoke)
    {
        // Arrange
        AdvisorHost? host = null;
        Func<Task> interfere = () => Task.CompletedTask;
        host = new AdvisorHost(wrapExecutor: inner => new AdvisorHost.InterleaveExecutor(inner, 3, () => interfere()));
        await using var _ = host;
        var handoffId = Uuid.CreateVersion4();
        var source = AdvisorHost.NewSource();
        var feedbackId = Uuid.CreateVersion4();
        Assert.True((await host.CreateAsync(handoffId, [source])).IsSuccess);
        Assert.True((await host.ReadyAsync(handoffId, 1)).IsSuccess);
        interfere = async () => Assert.True((await host.RecordAsync(host.Feedback(handoffId, feedbackId, source, 2))).IsSuccess);

        // Act
        var result = await host.EndAsync(handoffId, 2, revoke);
        await host.MaterializeAsync(handoffId);
        var saved = await host.HandoffAsync(handoffId);
        var thread = await host.ThreadAsync(handoffId, feedbackId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal("ready_for_review", saved.Status);
        Assert.Single(saved.Admitted);
        Assert.True(thread.IsMaterialized);
    }

    [Fact]
    public async Task ShouldRefuseAdmissionGivenHandoffNotReadyClosedOrExpired()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var draftId = Uuid.CreateVersion4();
        var source = AdvisorHost.NewSource();
        Assert.True((await host.CreateAsync(draftId, [source])).IsSuccess);
        var (closedId, closedSource) = await host.SeedReadyAsync();
        Assert.True((await host.EndAsync(closedId, 2, revoke: false)).IsSuccess);
        var (expiringId, expiringSource) = await host.SeedReadyAsync();
        host.Clock.Advance(TimeSpan.FromDays(400));

        // Act
        var draft = await host.RecordAsync(host.Feedback(draftId, Uuid.CreateVersion4(), source, 1));
        var closed = await host.RecordAsync(host.Feedback(closedId, Uuid.CreateVersion4(), closedSource, 3));
        var expired = await host.RecordAsync(host.Feedback(expiringId, Uuid.CreateVersion4(), expiringSource, 2));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, draft.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, closed.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, expired.Error?.Kind);
    }

    [Fact]
    public async Task ShouldRejectFeedbackGivenTargetOutsideSharedExactSelection()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, source) = await host.SeedReadyAsync();
        var otherVersion = new AdvisorRecordVersion(source.RecordId, Uuid.CreateVersion4());

        // Act
        var result = await host.RecordAsync(host.Feedback(handoffId, Uuid.CreateVersion4(), otherVersion, 2));
        var unsupported = await host.RecordAsync(host.Feedback(handoffId, Uuid.CreateVersion4(), source, 2,
            kind: "approval"));
        var saved = await host.HandoffAsync(handoffId);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, unsupported.Error?.Kind);
        Assert.Empty(saved.Admitted);
        Assert.Equal(2, saved.Revision);
    }

    [Fact]
    public async Task ShouldRejectFeedbackGivenMissingIdentityOrInconsistentSource()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, source) = await host.SeedReadyAsync();
        var policy = AdvisorRecordVersion.PolicyVersion(Uuid.CreateVersion4(), 1, 1);

        // Act
        var noFeedbackId = await host.RecordAsync(host.Feedback(handoffId, Uuid.Empty, source, 2));
        var noVersion = await host.RecordAsync(host.Feedback(handoffId, Uuid.CreateVersion4(),
            new AdvisorRecordVersion(source.RecordId, Uuid.Empty), 2));
        var mismatched = await host.RecordAsync(host.Feedback(handoffId, Uuid.CreateVersion4(), source, 2) with
        {
            RecordId = Uuid.CreateVersion4(),
            SelectedSource = policy,
        });
        var blank = await host.RecordAsync(host.Feedback(handoffId, Uuid.CreateVersion4(), source, 2, "  "));
        var negative = await host.RecordAsync(host.Feedback(handoffId, Uuid.CreateVersion4(), source, -1));

        // Assert
        Assert.All(new[] { noFeedbackId, noVersion, mismatched, blank, negative },
            result => Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind));
    }

    [Fact]
    public async Task ShouldRejectSystemDispatchGivenAdvisorFeedback()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, source) = await host.SeedReadyAsync();

        // Act
        var result = await host.RecordHandler.HandleAsync(AdvisorHost.System(host.Feedback(handoffId, Uuid.CreateVersion4(),
            source, 2)), CancellationToken.None);
        var saved = await host.HandoffAsync(handoffId);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Empty(saved.Admitted);
    }

    [Fact]
    public async Task ShouldNotRecordFeedbackGivenDifferentProgramScope()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, source) = await host.SeedReadyAsync();
        var request = host.Feedback(handoffId, Uuid.CreateVersion4(), source, 2) with { ProgramId = Uuid.CreateVersion4() };

        // Act
        var result = await host.RecordAsync(request);
        var saved = await host.HandoffAsync(handoffId);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Empty(saved.Admitted);
    }

    [Fact]
    public async Task ShouldKeepPriorGenerationFeedbackHistoricalGivenSelectionRevision()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, feedbackId, source) = await host.SeedFeedbackAsync();
        var replacement = AdvisorHost.NewSource();
        Assert.True((await host.ReviseAsync(handoffId, 3, [replacement])).IsSuccess);
        Assert.True((await host.ReadyAsync(handoffId, 4)).IsSuccess);

        // Act
        var page = await host.ListFeedbackHandler.HandleAsync(host.Http(new ListAdvisorFeedback(host.TenantId,
            host.ProgramId, handoffId)), CancellationToken.None);
        var stale = await host.RecordAsync(host.Feedback(handoffId, Uuid.CreateVersion4(), source, 5));
        var saved = await host.HandoffAsync(handoffId);

        // Assert
        var view = Assert.Single(page.Value.Items);
        Assert.Equal(feedbackId, view.AdvisorFeedbackId);
        Assert.False(view.IsCurrent);
        Assert.Equal(RequestErrorKind.Validation, stale.Error?.Kind);
        Assert.Equal(2, saved.Generation);
        Assert.Equal(1, Assert.Single(saved.Admitted).Generation);
    }

    [Fact]
    public async Task ShouldRecoverWithoutDuplicateAdmissionGivenUnknownAppendOutcome()
    {
        // Arrange
        await using var host = new AdvisorHost(wrapExecutor: inner => new AdvisorHost.CommitThenFailOnceExecutor(inner, 3));
        var handoffId = Uuid.CreateVersion4();
        var source = AdvisorHost.NewSource();
        Assert.True((await host.CreateAsync(handoffId, [source])).IsSuccess);
        Assert.True((await host.ReadyAsync(handoffId, 1)).IsSuccess);
        var feedbackId = Uuid.CreateVersion4();

        // Act
        var result = await host.RecordAsync(host.Feedback(handoffId, feedbackId, source, 2));
        var saved = await host.HandoffAsync(handoffId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(feedbackId, result.Value.OperationId);
        Assert.Single(saved.Admitted);
        Assert.Equal(3, saved.Revision);
    }

    [Fact]
    public async Task ShouldMaterializeExactlyOnceGivenReactorDeliveredTwiceAfterRevocation()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, source) = await host.SeedReadyAsync();
        var feedbackId = Uuid.CreateVersion4();
        Assert.True((await host.RecordAsync(host.Feedback(handoffId, feedbackId, source, 2))).IsSuccess);
        Assert.True((await host.EndAsync(handoffId, 3, revoke: true)).IsSuccess);
        var handoff = await host.HandoffAsync(handoffId);
        DomainEventRecord? record = null;
        await foreach (var candidate in host.Events.ReadAsync(handoff.Stream, 0))
            if (candidate.Event is AdvisorFeedbackAdmitted)
                record = candidate;
        await using var scope = host.Provider.CreateAsyncScope();
        var reactor = new AdvisorFeedbackMaterializationReactor(new InMemoryProjectionCheckpointStore(),
            scope.ServiceProvider.GetRequiredService<ProcessRootReaction>());

        // Act
        await reactor.HandleAsync(AdministratorTransitionHost.ReactionOf<AdvisorFeedbackAdmitted>(record!),
            CancellationToken.None);
        var once = await host.ThreadAsync(handoffId, feedbackId);
        await reactor.HandleAsync(AdministratorTransitionHost.ReactionOf<AdvisorFeedbackAdmitted>(record!),
            CancellationToken.None);
        var twice = await host.ThreadAsync(handoffId, feedbackId);
        var process = await scope.ServiceProvider.GetRequiredService<IProcessRecordStore>()
            .ReadAsync(await host.KeyAsync(feedbackId));

        // Assert
        Assert.True(once.IsMaterialized);
        Assert.Equal(1, twice.Revision);
        Assert.Equal(once.Version, twice.Version);
        Assert.Equal(ProcessState.Completed, process!.State);
        Assert.Equal(record!.Event.Metadata.EventId, process.Source.EventId);
    }

    [Fact]
    public async Task ShouldNotDuplicateMaterializationGivenReceiverAppendBeforeCheckpointCrash()
    {
        // Arrange
        var crash = new CrashingProcessStore();
        await using var host = new AdvisorHost(store: crash.Wrap);
        var (handoffId, source) = await host.SeedReadyAsync();
        var feedbackId = Uuid.CreateVersion4();
        Assert.True((await host.RecordAsync(host.Feedback(handoffId, feedbackId, source, 2))).IsSuccess);
        // The advisor receiver is the first of the stage: the lease takes one checkpoint, the receiver append follows.
        crash.ArmAfterReplaceCount(1);

        // Act
        var crashed = await Record.ExceptionAsync(() => host.MaterializeAsync(handoffId));
        var appended = await host.ThreadAsync(handoffId, feedbackId);
        crash.Disarm();
        var resumed = await host.MaterializeAsync(handoffId);
        var thread = await host.ThreadAsync(handoffId, feedbackId);

        // Assert
        Assert.IsType<InvalidOperationException>(crashed);
        Assert.True(appended.IsMaterialized);
        Assert.Equal(ProcessState.Completed, Assert.Single(resumed).State);
        Assert.Equal(1, thread.Revision);
        Assert.Single(await host.EventsAsync<AdvisorFeedbackRecorded>(thread.Stream));
    }

    [Fact]
    public async Task ShouldReplayOriginalFactGivenReceiverDeliveredTwiceWithIdenticalIds()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, feedbackId, _) = await host.SeedFeedbackAsync();
        var command = await ReceiverCommandAsync(host, handoffId, feedbackId);

        // Act
        var replayed = await host.MaterializeHandler.HandleAsync(
            AdvisorHost.System(command), CancellationToken.None);
        var thread = await host.ThreadAsync(handoffId, feedbackId);

        // Assert
        Assert.IsType<ReceiverOutcome.Replayed>(replayed.Value);
        Assert.Equal(1, thread.Revision);
    }

    [Fact]
    public async Task ShouldConflictGivenReceiverForChangedIntentOnTheSameEffectKey()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, source) = await host.SeedReadyAsync();
        var firstId = Uuid.CreateVersion4();
        var secondId = Uuid.CreateVersion4();
        Assert.True((await host.RecordAsync(host.Feedback(handoffId, firstId, source, 2))).IsSuccess);
        Assert.True((await host.RecordAsync(host.Feedback(handoffId, secondId, source, 3, "Second body."))).IsSuccess);
        var handoff = await host.HandoffAsync(handoffId);
        var first = handoff.Admitted[0];
        var second = handoff.Admitted[1];
        var firstThread = await host.ThreadAsync(handoffId, firstId);
        var forged = new MaterializeAdvisorFeedback(Uuid.CreateVersion4(), NativeFactReferenceOf(handoff, first),
            firstThread.Stream, first.Operation.RootOperationId,
            first.Generation, null, second.Intent, handoffId, firstId);

        // Act
        var result = await host.MaterializeHandler.HandleAsync(AdvisorHost.System(forged), CancellationToken.None);
        var thread = await host.ThreadAsync(handoffId, firstId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.False(result.Error!.IsTransient);
        Assert.False(thread.IsMaterialized);
    }

    [Fact]
    public async Task ShouldRetryGivenAdmissionNotVisibleOnTheHandoffYet()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, feedbackId, _) = await host.SeedFeedbackAsync();
        var command = await ReceiverCommandAsync(host, handoffId, feedbackId);
        var unseen = command with { Source = command.Source with { EventId = Uuid.CreateVersion4() } };
        var otherThread = Uuid.CreateVersion4();
        var other = command with { FeedbackId = otherThread };

        // Act
        var transient = await host.MaterializeHandler.HandleAsync(AdvisorHost.System(unseen), CancellationToken.None);
        var foreign = await host.MaterializeHandler.HandleAsync(AdvisorHost.System(other), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, transient.Error?.Kind);
        Assert.True(transient.Error!.IsTransient);
        Assert.Equal(RequestErrorKind.Conflict, foreign.Error?.Kind);
        Assert.False(foreign.Error!.IsTransient);
    }

    [Fact]
    public async Task ShouldRejectReceiverGivenNonSystemActor()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var authorizer = new AdvisorReceiverAuthorizer();
        var (handoffId, feedbackId, _) = await host.SeedFeedbackAsync();
        var command = await ReceiverCommandAsync(host, handoffId, feedbackId);

        // Act
        var personal = await authorizer.AuthorizeAsync(new Cntryl.Portia.RequestContext<IAdvisorReceiver>(command,
            new RequestDispatchContext(AdvisorHost.Personal(host.UserId))), CancellationToken.None);
        var system = await authorizer.AuthorizeAsync(new Cntryl.Portia.RequestContext<IAdvisorReceiver>(command,
            RequestActor.System), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, personal.Error?.Kind);
        Assert.True(system.IsSuccess);
    }

    static NativeFactReference NativeFactReferenceOf(AdvisorHandoffSnapshot handoff, AdvisorFeedbackAdmitted admitted) =>
        NativeFactReference.Of(handoff.Stream, admitted);

    static async Task<MaterializeAdvisorFeedback> ReceiverCommandAsync(AdvisorHost host, Uuid handoffId,
        Uuid feedbackId)
    {
        var handoff = await host.HandoffAsync(handoffId);
        var admitted = handoff.Admitted.Single(item => item.Operation.LogicalId == feedbackId);
        var thread = await host.ThreadAsync(handoffId, feedbackId);
        var recorded = thread.Recorded ?? throw new InvalidOperationException("Not materialized.");
        return new MaterializeAdvisorFeedback(recorded.Operation.LogicalId, NativeFactReferenceOf(handoff, admitted),
            thread.Stream, admitted.Operation.RootOperationId, admitted.Generation, null, admitted.Intent, handoffId,
            feedbackId);
    }
}
