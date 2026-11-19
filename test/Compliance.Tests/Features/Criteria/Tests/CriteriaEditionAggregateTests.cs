using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Kernel.Services;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaEditionAggregateTests
{
    static readonly ActorAttribution Actor = new("operator", "issuer");
    static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
    const string Label = "2017_tsc_2022_pof";

    [Fact]
    public void ShouldReplayLegacyDraftButKeepItsPendingParticipantUnavailable()
    {
        // Arrange
        var operation = Uuid.CreateVersion4();
        var owner = new CriteriaEdition(Uuid.CreateVersion4(), operation);
        var request = new DraftCriteriaEdition(owner.Id, operation, "tsc", Label);
        var current = Admission(owner, operation, request);
        var legacy = current with { FormatVersion = 0, PolicyContentSha256 = null };
        var scenario = new AggregateScenario<CriteriaEdition>(owner);
        Assert.False(owner.Draft(operation, "tsc", Label, Actor, legacy).IsSuccess);
        Assert.Empty(scenario.PendingEvents);
        Assert.True(owner.Draft(operation, "tsc", Label, Actor, current).IsSuccess);
        var requested = Assert.IsType<CriteriaEditionDraftRequested>(scenario.PendingEvents.Single());
        var historical = requested with { Admission = legacy };
        var replay = new CriteriaEdition(owner.Id, operation);
        new AggregateScenario<CriteriaEdition>(replay).Given([historical]);

        // Act
        var outcome = replay.ActivateDraft(operation, 1, Uuid.CreateVersion4());

        // Assert
        Assert.False(outcome.IsSuccess);
        Assert.Equal("pending", replay.Receipt(operation)!.Status);
        Assert.Empty(new AggregateScenario<CriteriaEdition>(replay).PendingEvents);
    }

    [Fact]
    public void ShouldRemainPendingUntilTheExactLabelConfirmation()
    {
        // Arrange
        var operation = Uuid.CreateVersion4();
        var owner = new CriteriaEdition(Uuid.CreateVersion4(), operation);
        var request = new DraftCriteriaEdition(owner.Id, operation, "tsc", Label);
        Assert.True(owner.Draft(operation, "tsc", Label, Actor, Admission(owner, operation, request)).IsSuccess);

        // Act
        Assert.True(owner.ActivateDraft(operation, 1, Uuid.CreateVersion4()).IsSuccess);

        // Assert
        Assert.Equal("activating", owner.Status);
        Assert.Equal("tsc", owner.Framework);
        Assert.Equal("pending", owner.Receipt(operation)!.Status);
        Assert.True(owner.CompleteDraft(operation, 1, Uuid.CreateVersion4()).IsSuccess);
        Assert.Equal("draft", owner.Status);
        Assert.Equal("accepted", owner.Receipt(operation)!.Status);
        var facts = new AggregateScenario<CriteriaEdition>(owner).PendingEvents.ToArray();
        Assert.Equal("tsc", Assert.IsType<CriteriaEditionDraftRequested>(facts[0]).Framework);
        Assert.Equal("tsc", Assert.IsType<CriteriaEditionDraftActivated>(facts[1]).Framework);
        Assert.Equal("tsc", Assert.IsType<CriteriaEditionDrafted>(facts[2]).Framework);
    }

    [Fact]
    public void ShouldRejectUnsupportedOrMismatchedFrameworkGivenDraftRequest()
    {
        // Arrange
        var operation = Uuid.CreateVersion4();
        var owner = new CriteriaEdition(Uuid.CreateVersion4(), operation);
        var valid = new DraftCriteriaEdition(owner.Id, operation, "tsc", Label);
        var other = valid with { Framework = "other" };
        var scenario = new AggregateScenario<CriteriaEdition>(owner);

        // Act
        var unsupported = owner.Draft(operation, other.Framework, Label, Actor, Admission(owner, operation, other));
        var mismatchedAdmission = owner.Draft(operation, valid.Framework, Label, Actor, Admission(owner, operation, other));

        // Assert
        Assert.False(unsupported.IsSuccess);
        Assert.False(mismatchedAdmission.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
        Assert.Null(owner.Framework);
    }

    [Fact]
    public void ShouldRejectMismatchedFrameworkGivenDraftActivationReplay()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var owner = new CriteriaEdition(edition, operation);
        var request = new DraftCriteriaEdition(edition, operation, "tsc", Label);
        Assert.True(owner.Draft(operation, "tsc", Label, Actor, Admission(owner, operation, request)).IsSuccess);
        Assert.True(owner.ActivateDraft(operation, 1, Uuid.CreateVersion4()).IsSuccess);
        var facts = new AggregateScenario<CriteriaEdition>(owner).PendingEvents.ToArray();
        var activation = Assert.IsType<CriteriaEditionDraftActivated>(facts[1]);
        var corrupted = activation with { Framework = "other" };

        // Act
        var failure = Record.Exception(() =>
            new AggregateScenario<CriteriaEdition>(new CriteriaEdition(edition, operation)).Given([facts[0], corrupted]));

        // Assert
        Assert.IsType<InvalidOperationException>(failure);
    }

    [Fact]
    public void ShouldReplayExactDraftAndRejectChangedIntentWithoutNewFacts()
    {
        // Arrange
        var operation = Uuid.CreateVersion4();
        var owner = new CriteriaEdition(Uuid.CreateVersion4(), operation);
        var request = new DraftCriteriaEdition(owner.Id, operation, "tsc", Label);
        Assert.True(owner.Draft(operation, "tsc", Label, Actor, Admission(owner, operation, request)).IsSuccess);
        var scenario = new AggregateScenario<CriteriaEdition>(owner);
        var before = scenario.PendingEvents.ToArray();
        var changed = request with { Label = "2017_tsc_2023_pof" };

        // Act
        var exact = owner.Draft(operation, "tsc", Label, Actor, Admission(owner, operation, request));
        var conflict = owner.Draft(operation, "tsc", changed.Label, Actor, Admission(owner, operation, changed));

        // Assert
        Assert.True(exact.IsSuccess);
        Assert.False(conflict.IsSuccess);
        Assert.Equal(before, scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRejectStaleAuthorizationAfterRecoveryFence()
    {
        // Arrange
        var operation = Uuid.CreateVersion4();
        var owner = new CriteriaEdition(Uuid.CreateVersion4(), operation);
        var request = new DraftCriteriaEdition(owner.Id, operation, "tsc", Label);
        var digest = AuthoredIntentDigest.Compute(request);
        var stale = Admission(owner, operation, request, 1);
        Assert.True(owner.FenceOperationAttempt(operation, 1, 0, digest).IsSuccess);
        var scenario = new AggregateScenario<CriteriaEdition>(owner);
        var before = scenario.PendingEvents.ToArray();

        // Act
        var rejected = owner.Draft(operation, "tsc", Label, Actor, stale, 1);

        // Assert
        Assert.False(rejected.IsSuccess);
        Assert.Equal(before, scenario.PendingEvents);
        Assert.Equal(2, owner.NextExecutionGeneration);
        Assert.True(owner.Draft(operation, "tsc", Label, Actor, Admission(owner, operation, request, 2), 2).IsSuccess);
    }

    [Fact]
    public void ShouldKeepAbortPendingUntilTheWitnessFenceCommits()
    {
        // Arrange
        var attempt = Uuid.CreateVersion4();
        var owner = ReadyDraft(attempt);
        var witness = Uuid.CreateVersion4();
        var request = new BeginCriteriaPublication(owner.Id, attempt, "CC6.1", witness);
        Assert.True(owner.BeginPublication(attempt, "CC6.1", witness, Actor, Admission(owner, attempt, request)).IsSuccess);
        Assert.True(owner.AbortPublication(attempt, "witness unavailable").IsSuccess);
        var scenario = new AggregateScenario<CriteriaEdition>(owner);
        var before = scenario.PendingEvents.ToArray();

        // Act
        var latePublish = owner.Publish(attempt, Uuid.CreateVersion4());
        var missingFence = owner.ConfirmPublicationAbortCleanup(attempt, Uuid.Empty);

        // Assert
        Assert.False(latePublish.IsSuccess);
        Assert.False(missingFence.IsSuccess);
        Assert.Equal("abort_cleanup", owner.Status);
        Assert.Equal(before, scenario.PendingEvents);
        Assert.True(owner.ConfirmPublicationAbortCleanup(attempt, Uuid.CreateVersion4()).IsSuccess);
        Assert.Equal("draft", owner.Status);
        Assert.True(owner.AbortPublication(attempt, "witness unavailable").IsSuccess);
    }

    [Fact]
    public void ShouldRejectSameLabelRenameWithoutReturningAnUnobservableSuccess()
    {
        // Arrange
        var operation = Uuid.CreateVersion4();
        var owner = ReadyDraft(operation);
        var request = new BeginCriteriaEditionRename(owner.Id, operation, Label);
        var scenario = new AggregateScenario<CriteriaEdition>(owner);
        var before = scenario.PendingEvents.ToArray();

        // Act
        var result = owner.BeginRename(operation, Label, Actor, Admission(owner, operation, request));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Null(owner.Receipt(operation));
        Assert.Equal(before, scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRecoverOnlyTheSelectedHistoricalOperationGivenMultipleLabelAttempts()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var initialOperation = Uuid.CreateVersion4();
        var initial = new CriteriaEdition(edition, initialOperation);
        var request = new DraftCriteriaEdition(edition, initialOperation, "tsc", Label);
        Assert.True(initial.Draft(initialOperation, "tsc", Label, Actor, Admission(initial, initialOperation, request)).IsSuccess);
        var refusal = Uuid.CreateVersion4();
        Assert.True(initial.RecordInitialLabelRefusal(initialOperation, refusal, "label unavailable").IsSuccess);
        var initialHistory = new AggregateScenario<CriteriaEdition>(initial).PendingEvents.ToArray();
        var replacementOperation = Uuid.CreateVersion4();
        var replacement = new CriteriaEdition(edition, replacementOperation);
        new AggregateScenario<CriteriaEdition>(replacement).Given(initialHistory);
        var replacementRequest = new ChooseInitialEditionLabel(edition, replacementOperation, "2017_tsc_2023_pof");
        Assert.True(replacement.ChooseInitialLabel(replacementOperation, replacementRequest.Label, Actor,
            Admission(replacement, replacementOperation, replacementRequest)).IsSuccess);
        var laterHistory = initialHistory.Concat(new AggregateScenario<CriteriaEdition>(replacement).PendingEvents).ToArray();
        var replay = new CriteriaEdition(edition, initialOperation);
        new AggregateScenario<CriteriaEdition>(replay).Given(laterHistory);

        // Act
        var receipt = replay.Receipt(initialOperation);
        var unrelated = replay.Receipt(replacementOperation);
        var exactRetry = replay.Draft(initialOperation, "tsc", Label, Actor, Admission(replay, initialOperation, request));
        var changedRequest = request with { Label = "2017_tsc_2024_pof" };
        var changedRetry = replay.Draft(initialOperation, "tsc", changedRequest.Label, Actor,
            Admission(replay, initialOperation, changedRequest));
        var exactRefusal = replay.RecordInitialLabelRefusal(initialOperation, refusal, "label unavailable");
        var changedRefusal = replay.RecordInitialLabelRefusal(initialOperation, refusal, "another reason");

        // Assert
        Assert.Equal("refused", receipt!.Status);
        Assert.Equal("label unavailable", receipt.Reason);
        Assert.Null(unrelated);
        Assert.True(exactRetry.IsSuccess);
        Assert.False(changedRetry.IsSuccess);
        Assert.True(exactRefusal.IsSuccess);
        Assert.False(changedRefusal.IsSuccess);
        Assert.Equal("pending", replay.Status);
    }

    [Fact]
    public void ShouldRefuseMutationGivenAnUnselectedOrDifferentOperation()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var request = new DraftCriteriaEdition(edition, operation, "tsc", Label);
        var unselected = new CriteriaEdition(edition);
        var other = new CriteriaEdition(edition, Uuid.CreateVersion4());

        // Act
        var withoutSelection = unselected.Draft(operation, "tsc", Label, Actor, Admission(unselected, operation, request));
        var differentSelection = other.Draft(operation, "tsc", Label, Actor, Admission(other, operation, request));

        // Assert
        Assert.False(withoutSelection.IsSuccess);
        Assert.False(differentSelection.IsSuccess);
        Assert.Empty(new AggregateScenario<CriteriaEdition>(unselected).PendingEvents);
        Assert.Empty(new AggregateScenario<CriteriaEdition>(other).PendingEvents);
    }

    [Fact]
    public void ShouldRecoverBothReceiptsIndependentlyGivenADraftCancellation()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var draftOperation = Uuid.CreateVersion4();
        var original = new CriteriaEdition(edition, draftOperation);
        var draft = new DraftCriteriaEdition(edition, draftOperation, "tsc", Label);
        Assert.True(original.Draft(draftOperation, "tsc", Label, Actor, Admission(original, draftOperation, draft)).IsSuccess);
        var firstEvents = new AggregateScenario<CriteriaEdition>(original).PendingEvents.ToArray();
        var cancelOperation = Uuid.CreateVersion4();
        var cancelling = new CriteriaEdition(edition, cancelOperation);
        new AggregateScenario<CriteriaEdition>(cancelling).Given(firstEvents);
        var cancellation = new CancelCriteriaEditionDraftRequest(edition, cancelOperation, draftOperation);
        Assert.True(cancelling.CancelDraftRequest(cancelOperation, draftOperation, Actor,
            Admission(cancelling, cancelOperation, cancellation)).IsSuccess);
        var history = firstEvents.Concat(new AggregateScenario<CriteriaEdition>(cancelling).PendingEvents).ToArray();

        // Act
        var originalReplay = new CriteriaEdition(edition, draftOperation);
        new AggregateScenario<CriteriaEdition>(originalReplay).Given(history);
        var cancellationReplay = new CriteriaEdition(edition, cancelOperation);
        new AggregateScenario<CriteriaEdition>(cancellationReplay).Given(history);

        // Assert
        Assert.Equal("refused", originalReplay.Receipt(draftOperation)!.Status);
        Assert.Equal("cancelled", originalReplay.Receipt(draftOperation)!.Reason);
        Assert.Null(originalReplay.Receipt(cancelOperation));
        Assert.Equal("accepted", cancellationReplay.Receipt(cancelOperation)!.Status);
        Assert.Null(cancellationReplay.Receipt(draftOperation));
        Assert.Equal("abandoned", originalReplay.Status);
        Assert.Equal("abandoned", cancellationReplay.Status);
    }

    [Fact]
    public void ShouldKeepExactParticipantRetryGivenTheSelectedOperationWasCompleted()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var owner = new CriteriaEdition(edition, operation);
        var request = new DraftCriteriaEdition(edition, operation, "tsc", Label);
        Assert.True(owner.Draft(operation, "tsc", Label, Actor, Admission(owner, operation, request)).IsSuccess);
        var reservation = Uuid.CreateVersion4();
        var confirmation = Uuid.CreateVersion4();
        Assert.True(owner.ActivateDraft(operation, 1, reservation).IsSuccess);
        Assert.True(owner.CompleteDraft(operation, 1, confirmation).IsSuccess);
        var history = new AggregateScenario<CriteriaEdition>(owner).PendingEvents.ToArray();
        var replay = new CriteriaEdition(edition, operation);
        var scenario = new AggregateScenario<CriteriaEdition>(replay).Given(history);

        // Act
        var exact = replay.ActivateDraft(operation, 1, reservation);
        var changed = replay.ActivateDraft(operation, 1, Uuid.CreateVersion4());
        var changedGeneration = replay.ActivateDraft(operation, 2, reservation);
        var changedConfirmationGeneration = replay.CompleteDraft(operation, 2, confirmation);
        var unselected = replay.ActivateDraft(Uuid.CreateVersion4(), 1, reservation);

        // Assert
        Assert.True(exact.IsSuccess);
        Assert.False(changed.IsSuccess);
        Assert.False(changedGeneration.IsSuccess);
        Assert.False(changedConfirmationGeneration.IsSuccess);
        Assert.False(unselected.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRejectReusingADraftIdentityForARenameGivenACompletedDraft()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var owner = new CriteriaEdition(edition, operation);
        var draft = new DraftCriteriaEdition(edition, operation, "tsc", Label);
        Assert.True(owner.Draft(operation, "tsc", Label, Actor, Admission(owner, operation, draft)).IsSuccess);
        Assert.True(owner.ActivateDraft(operation, 1, Uuid.CreateVersion4()).IsSuccess);
        Assert.True(owner.CompleteDraft(operation, 1, Uuid.CreateVersion4()).IsSuccess);
        var before = new AggregateScenario<CriteriaEdition>(owner).PendingEvents.ToArray();
        var rename = new BeginCriteriaEditionRename(edition, operation, "2017_tsc_2023_pof");

        // Act
        var result = owner.BeginRename(operation, rename.Label, Actor, Admission(owner, operation, rename));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(before, new AggregateScenario<CriteriaEdition>(owner).PendingEvents);
    }

    static CriteriaEdition ReadyDraft(Uuid selectedOperation)
    {
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var owner = new CriteriaEdition(edition, operation);
        var request = new DraftCriteriaEdition(owner.Id, operation, "tsc", Label);
        Assert.True(owner.Draft(operation, "tsc", Label, Actor, Admission(owner, operation, request)).IsSuccess);
        Assert.True(owner.ActivateDraft(operation, 1, Uuid.CreateVersion4()).IsSuccess);
        Assert.True(owner.CompleteDraft(operation, 1, Uuid.CreateVersion4()).IsSuccess);
        var selected = new CriteriaEdition(edition, selectedOperation);
        new AggregateScenario<CriteriaEdition>(selected).Given(new AggregateScenario<CriteriaEdition>(owner).PendingEvents.ToArray());
        return selected;
    }

    static AccessAdmissionEvidence Admission<T>(CriteriaEdition owner, Uuid operation, T request, long generation = 1)
        where T : notnull
    {
        var identity = new AccessAdmissionSourceVersion("stream://bdgrz/identities/actor", "generation", 1);
        var policy = new AccessAdmissionSourceVersion("stream://bdgrz/policies/current", "generation", 1);
        return new AccessAdmissionEvidence(Uuid.CreateVersion4(), Actor.Subject, Actor.Issuer, operation,
            owner.Stream.ToString(), CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(request), generation), Now,
            "policy-v1", identity, policy)
        {
            FormatVersion = 1,
            PolicyContentSha256 = new string('a', 64),
            Operator = new AccessAdmissionSourceVersion("stream://bdgrz/operators/actor", "generation", 1)
        };
    }
}
