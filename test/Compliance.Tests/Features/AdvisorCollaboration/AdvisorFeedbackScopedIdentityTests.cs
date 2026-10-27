using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Aggregates;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.SourceUse;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AdvisorCollaboration;

public sealed class AdvisorFeedbackScopedIdentityTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldKeepBothFeedbackThreadsAndResolveOnlyTargetGivenSameFeedbackIdOnTwoHandoffs(bool sameProgram)
    {
        // Arrange
        await using var source = new OwnerScopedProjectionScenario();
        var tenant = Uuid.CreateVersion4();
        var firstProgram = Uuid.CreateVersion4();
        var secondProgram = sameProgram ? firstProgram : Uuid.CreateVersion4();
        var firstHandoff = Uuid.CreateVersion4();
        var secondHandoff = Uuid.CreateVersion4();
        var shared = Uuid.CreateVersion4();
        var first = await MaterializeAsync(source, tenant, firstProgram, firstHandoff, shared);
        await MaterializeAsync(source, tenant, secondProgram, secondHandoff, shared);
        var directory = new FitzAdvisorFeedbackWorkItemDirectory(source.Kv, source.Content);
        var identity = new CheckpointIdentity(FitzAdvisorFeedbackWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenant.ToString(), AdvisorFeedbackThread.Area));

        // Act
        await OwnerScopedProjectionScenario.ProjectAsync(directory, directory.ApplyAsync, identity, await source.ReadAsync(tenant, AdvisorFeedbackThread.Area));
        var firstPending = await directory.LoadProgramAsync(tenant, firstProgram);
        var secondPending = sameProgram ? firstPending : await directory.LoadProgramAsync(tenant, secondProgram);
        var manager = ActorReference.ForMember(Uuid.CreateVersion4(), "Manager");
        var (response, result) = await AdvisorContent.FreezeUnitAsync(source.Content, tenant, CancellationToken.None);
        var firstResponse = await FirstResponses.FreezeAsync(source.Content, tenant.ToString(), response);
        var commandId = Uuid.CreateVersion4();
        first = await source.Reader.HydrateAsync(new AdvisorFeedbackThread(tenant, firstProgram, firstHandoff, shared, commandId));
        _ = await AdvisorNativeHistory.ReadAsync(first, source.Events, CancellationToken.None);
        var operation = OwnerProvenance.For(first, commandId, "ResolveAdvisorFeedback", manager, "initial", 1,
            new string('a', 64), Now.AddMinutes(1));
        Assert.True(first.Resolve(operation, manager, Now.AddMinutes(1), firstResponse, result).IsSuccess);
        await source.Writer.SaveAsync(first, new RequestDispatchContext(RequestActor.System));
        var resolved = Assert.Single((await source.ReadAsync(tenant, AdvisorFeedbackThread.Area)).OfType<AdvisorFeedbackResolved>());
        await OwnerScopedProjectionScenario.ProjectAsync(directory, directory.ApplyAsync, identity, [resolved]);
        var remainingFirst = await directory.LoadProgramAsync(tenant, firstProgram);
        var remainingSecond = sameProgram ? remainingFirst : await directory.LoadProgramAsync(tenant, secondProgram);

        // Assert
        Assert.True(firstPending.IsSuccess, firstPending.Error?.Message);
        Assert.True(secondPending.IsSuccess, secondPending.Error?.Message);
        var both = sameProgram ? firstPending.Value : [.. firstPending.Value, .. secondPending.Value];
        Assert.Equal(2, both.Count);
        Assert.Equal(2, both.Select(candidate => candidate.WorkItemId).Distinct().Count());
        Assert.Equal(sameProgram ? 1 : 0, remainingFirst.Value.Count);
        var remaining = Assert.Single(remainingSecond.Value);
        Assert.Equal(shared, remaining.SourceId);
        Assert.Contains($"advisor-handoffs/{secondHandoff}/feedback/{shared}", remaining.ActionPath, StringComparison.Ordinal);
        Assert.DoesNotContain($"advisor-handoffs/{firstHandoff}/", remaining.ActionPath, StringComparison.Ordinal);
    }

    static async Task<AdvisorFeedbackThread> MaterializeAsync(OwnerScopedProjectionScenario source, Uuid tenant,
        Uuid program, Uuid handoff, Uuid feedback)
    {
        var author = ActorReference.ForMember(Uuid.CreateVersion4(), "Advisor");
        var authored = new RecordAdvisorFeedback(tenant, program, handoff, feedback, Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), "change_request", "Clarify ownership", 1);
        var parent = new AdvisorHandoff(tenant, program, handoff).Stream;
        var header = new DurableIntentHeader(feedback, parent, parent, parent, author, "initial", 1, AuthoredIntentDigest.Compute(authored), Now);
        var authoredContent = await source.Content.WriteJsonAsync(tenant.ToString(), Uuid.CreateVersion4(),
            AdvisorContent.FeedbackAuthoredSchema, authored);
        var proofs = await AdvisorContent.EmptyProofsAsync(source.Content, tenant, feedback, CancellationToken.None);
        var intent = await source.Content.WriteJsonAsync(tenant.ToString(), Uuid.CreateVersion4(), ChildAdmissionIntent.Schema,
            new ChildAdmissionIntent(header, handoff, 1, AdmittedChildKind.AdvisorFeedback, feedback, authoredContent, proofs, 2));
        var owner = new AdvisorFeedbackThread(tenant, program, handoff, feedback);
        var operation = SourceUseReceiverSupport.Child(Uuid.CreateVersion4(), "MaterializeAdvisorFeedback", Uuid.CreateVersion4(),
            header, owner.Stream, Now);
        Assert.True(owner.Materialize(operation, authored, author, Now, intent).IsSuccess);
        await source.Writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System));
        return owner;
    }
}
