using System.Text.Json;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceFacts.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Tests.Testing;

namespace Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;

public sealed class MethodologyTemplateNameClaimTests
{
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    const string Name = "Firm methodology";

    [Fact]
    public void ShouldUseOneGlobalNameOwnerGivenEquivalentNormalizedNames()
    {
        // Arrange
        var first = new MethodologyTemplateNameClaim(" Firm Methodology ");
        var second = new MethodologyTemplateNameClaim("firm methodology");

        // Act
        var same = first.Stream == second.Stream;

        // Assert
        Assert.True(same);
        Assert.Equal(MethodologyTemplate.Realm, first.Stream.Realm);
        Assert.Equal("FIRM METHODOLOGY", first.NormalizedName);
    }

    [Fact]
    public void ShouldRefuseCompetingTargetWithoutFactGivenNameAlreadyClaimed()
    {
        // Arrange
        var first = Claimed();
        var second = Intent();
        var scenario = Scenario(second.CommandId).Given(first.Scenario.PendingEvents.ToArray());
        var content = Content(second, MethodologyTemplateNameClaim.CreationSchema);
        var operation = Root(scenario.Aggregate, second, content);

        // Act
        var result = scenario.Aggregate.ClaimCreation(operation, second, content, Response());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
        Assert.Empty(scenario.PendingEvents);
        Assert.Equal(Target(first.Intent.TemplateId), scenario.Aggregate.Holder);
    }

    [Fact]
    public void ShouldKeepExactRetryFactFreeAndRefuseChangedIntentGivenAcceptedCreation()
    {
        // Arrange
        var fixture = Claimed();
        var count = fixture.Scenario.PendingEvents.Count;
        var changed = fixture.Intent with { Purpose = "Changed purpose" };
        var changedContent = Content(changed, MethodologyTemplateNameClaim.CreationSchema);

        // Act
        var repeated = fixture.Scenario.Aggregate.ClaimCreation(fixture.Operation, fixture.Intent, fixture.Content, Response());
        var changedIntent = fixture.Scenario.Aggregate.ClaimCreation(Root(fixture.Scenario.Aggregate, changed, changedContent),
            changed, changedContent, Response());

        // Assert
        Assert.True(repeated.IsSuccess, repeated.Error?.Message);
        Assert.False(changedIntent.IsSuccess);
        Assert.Equal(count, fixture.Scenario.PendingEvents.Count);
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenSystemActorAuthoredRootClaim()
    {
        // Arrange
        var intent = Intent();
        var scenario = Scenario(intent.CommandId);
        var content = Content(intent, MethodologyTemplateNameClaim.CreationSchema);
        var operation = Root(scenario.Aggregate, intent, content) with
        {
            Actor = ActorReference.ForSystemProcess("not-an-operator", "System"),
        };

        // Act
        var result = scenario.Aggregate.ClaimCreation(operation, intent, content, Response());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
        Assert.Null(scenario.Aggregate.Holder);
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenFrozenCreationDoesNotNameThisAuthorityOrActor()
    {
        // Arrange
        var intent = Intent() with { Name = "Another methodology" };
        var scenario = Scenario(intent.CommandId);
        var content = Content(intent, MethodologyTemplateNameClaim.CreationSchema);
        var operation = Root(scenario.Aggregate, intent, content);

        // Act
        var wrongName = scenario.Aggregate.ClaimCreation(operation, intent, content, Response());
        var original = intent with { Name = Name };
        var originalContent = Content(original, MethodologyTemplateNameClaim.CreationSchema);
        var wrongActor = Root(scenario.Aggregate, original, originalContent) with
        {
            Actor = ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Another operator"),
        };
        var forgedActor = scenario.Aggregate.ClaimCreation(wrongActor, original, originalContent, Response());

        // Assert
        Assert.False(wrongName.IsSuccess);
        Assert.False(forgedActor.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
        Assert.Null(scenario.Aggregate.Holder);
    }

    [Fact]
    public void ShouldReleaseOnlyLosingNameGivenCommittedConflictingLineCreation()
    {
        // Arrange
        var claim = Claimed();
        var refusal = Refusal(claim, Existing(claim.Intent, "Another methodology"));
        var scenario = Scenario(refusal.Operation.LogicalId).Given(claim.Scenario.PendingEvents.ToArray());

        // Act
        var result = scenario.Aggregate.RefuseCreation(refusal.Operation, claim.Fact, claim.Intent, refusal.Existing,
            refusal.Proofs, refusal.Content);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Null(scenario.Aggregate.Holder);
        var fact = Assert.IsType<MethodologyTemplateCreationRefused>(Assert.Single(scenario.PendingEvents));
        Assert.True(fact.ReleasedName);
        Assert.Equal(claim.Fact.Metadata.EventId, fact.ClaimEventId);
        Assert.Equal(refusal.Existing.Metadata.EventId, fact.ExistingCreationEventId);
        Assert.Equal(refusal.Content, fact.SourceProofs);
    }

    [Fact]
    public void ShouldRecordRefusalAndRetainUsedNameGivenSameNameWithChangedCreationIntent()
    {
        // Arrange
        var claim = Claimed();
        var refusal = Refusal(claim, Existing(claim.Intent, Name));
        var scenario = Scenario(refusal.Operation.LogicalId).Given(claim.Scenario.PendingEvents.ToArray());

        // Act
        var result = scenario.Aggregate.RefuseCreation(refusal.Operation, claim.Fact, claim.Intent, refusal.Existing,
            refusal.Proofs, refusal.Content);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(Target(claim.Intent.TemplateId), scenario.Aggregate.Holder);
        Assert.False(Assert.IsType<MethodologyTemplateCreationRefused>(Assert.Single(scenario.PendingEvents)).ReleasedName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldRefuseFalseConflictWithoutFactGivenCommittedCreationMatchesIntent(bool differentEventId)
    {
        // Arrange
        var claim = Claimed();
        var existing = new MethodologyTemplateCreatedV4(claim.Intent.TemplateId, claim.Intent.Name,
            claim.Intent.Purpose, claim.Intent.CreatedBy);
        existing.AttachMetadata(new DomainEventMetadata(differentEventId ? Uuid.CreateVersion4() : claim.Fact.Metadata.EventId,
            claim.Intent.TemplateId, 1, Now));
        var refusal = Refusal(claim, existing);
        var scenario = Scenario(refusal.Operation.LogicalId).Given(claim.Scenario.PendingEvents.ToArray());

        // Act
        var result = scenario.Aggregate.RefuseCreation(refusal.Operation, claim.Fact, claim.Intent, existing,
            refusal.Proofs, refusal.Content);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
        Assert.Equal(Target(claim.Intent.TemplateId), scenario.Aggregate.Holder);
    }

    [Theory]
    [InlineData("claim_event")]
    [InlineData("creation_event")]
    [InlineData("realm")]
    [InlineData("payload")]
    [InlineData("content_digest")]
    [InlineData("creator")]
    public void ShouldRefuseWithoutFactGivenForgedConflictBinding(string fault)
    {
        // Arrange
        var claim = Claimed();
        var refusal = Refusal(claim, Existing(claim.Intent, "Another methodology"));
        var scenario = Scenario(refusal.Operation.LogicalId).Given(claim.Scenario.PendingEvents.ToArray());
        var proofs = fault switch
        {
            "claim_event" => refusal.Proofs with { Claim = refusal.Proofs.Claim with { EventId = Uuid.CreateVersion4() } },
            "creation_event" => refusal.Proofs with { ExistingCreation = refusal.Proofs.ExistingCreation with { EventId = Uuid.CreateVersion4() } },
            "realm" => refusal.Proofs with { ExistingCreation = refusal.Proofs.ExistingCreation with
            {
                Source = new EventStreamAddress("wrong-realm", MethodologyTemplate.Area, claim.Intent.TemplateId.ToString()),
            } },
            "payload" => refusal.Proofs with { ExistingCreation = refusal.Proofs.ExistingCreation with { PayloadSha256 = new string('0', 64) } },
            _ => refusal.Proofs,
        };
        var body = Content(proofs, MethodologyTemplateNameClaim.ConflictSchema);
        if (fault == "content_digest")
            body = body with { Sha256 = new string('0', 64) };
        var operation = refusal.Operation with { AuthoredIntentSha256 = body.Sha256 };
        var intent = fault == "creator" ? claim.Intent with { CreatedBy = Uuid.CreateVersion4() } : claim.Intent;

        // Act
        var result = scenario.Aggregate.RefuseCreation(operation, claim.Fact, intent, refusal.Existing, proofs, body);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
        Assert.Equal(Target(claim.Intent.TemplateId), scenario.Aggregate.Holder);
    }

    [Fact]
    public void ShouldKeepOldAdmissionAndRefusalRetriesFactFreeGivenNameReleasedAndAcquiredByAnotherLine()
    {
        // Arrange
        var original = Claimed();
        var refusal = Refusal(original, Existing(original.Intent, "Another methodology"));
        var released = Scenario(refusal.Operation.LogicalId).Given(original.Scenario.PendingEvents.ToArray());
        Assert.True(released.Aggregate.RefuseCreation(refusal.Operation, original.Fact, original.Intent,
            refusal.Existing, refusal.Proofs, refusal.Content).IsSuccess);
        var next = Intent();
        var nextContent = Content(next, MethodologyTemplateNameClaim.CreationSchema);
        var reacquired = Scenario(next.CommandId).Given(original.Scenario.PendingEvents.Concat(released.PendingEvents).ToArray());
        Assert.True(reacquired.Aggregate.ClaimCreation(Root(reacquired.Aggregate, next, nextContent), next,
            nextContent, Response()).IsSuccess);
        var replay = Scenario(refusal.Operation.LogicalId).Given(original.Scenario.PendingEvents.Concat(released.PendingEvents)
            .Concat(reacquired.PendingEvents).ToArray());

        // Act
        var repeated = replay.Aggregate.RefuseCreation(refusal.Operation, original.Fact, original.Intent,
            refusal.Existing, refusal.Proofs, refusal.Content);
        var rootReplay = Scenario(original.Intent.CommandId).Given(original.Scenario.PendingEvents.Concat(released.PendingEvents)
            .Concat(reacquired.PendingEvents).ToArray());
        var oldAdmission = rootReplay.Aggregate.ClaimCreation(original.Operation, original.Intent, original.Content, Response());

        // Assert
        Assert.True(repeated.IsSuccess, repeated.Error?.Message);
        Assert.True(oldAdmission.IsSuccess, oldAdmission.Error?.Message);
        Assert.Empty(replay.PendingEvents);
        Assert.Empty(rootReplay.PendingEvents);
        Assert.Equal(Target(next.TemplateId), replay.Aggregate.Holder);
        Assert.Equal(Target(next.TemplateId), rootReplay.Aggregate.Holder);
    }

    [Fact]
    public void ShouldPreserveNewerGenerationGivenOldClaimRefusalArrivesAfterRepin()
    {
        // Arrange
        var original = Claimed();
        var next = original.Intent with { CommandId = Uuid.CreateVersion4(), Purpose = "Second intent" };
        var nextContent = Content(next, MethodologyTemplateNameClaim.CreationSchema);
        var repinned = Scenario(next.CommandId).Given(original.Scenario.PendingEvents.ToArray());
        Assert.True(repinned.Aggregate.ClaimCreation(Root(repinned.Aggregate, next, nextContent), next,
            nextContent, Response()).IsSuccess);
        var refusal = Refusal(original, Existing(original.Intent, "Another methodology"));
        var scenario = Scenario(refusal.Operation.LogicalId).Given(original.Scenario.PendingEvents.Concat(repinned.PendingEvents).ToArray());
        var generation = scenario.Aggregate.HeldGeneration;

        // Act
        var refused = scenario.Aggregate.RefuseCreation(refusal.Operation, original.Fact, original.Intent,
            refusal.Existing, refusal.Proofs, refusal.Content);

        // Assert
        Assert.True(refused.IsSuccess, refused.Error?.Message);
        Assert.Equal(Target(next.TemplateId), scenario.Aggregate.Holder);
        Assert.Equal(generation, scenario.Aggregate.HeldGeneration);
        Assert.False(Assert.IsType<MethodologyTemplateCreationRefused>(Assert.Single(scenario.PendingEvents)).ReleasedName);
    }

    [Fact]
    public async Task ShouldLetOneAppendWinGivenCompetingHydratedNameClaims()
    {
        // Arrange
        await using var store = new MethodologyTemplateScenario(Now);
        var first = Intent();
        var second = Intent();
        var left = await store.Reader.HydrateAsync(new MethodologyTemplateNameClaim(Name, first.CommandId));
        var right = await store.Reader.HydrateAsync(new MethodologyTemplateNameClaim(Name, second.CommandId));
        var leftContent = Content(first, MethodologyTemplateNameClaim.CreationSchema);
        var rightContent = Content(second, MethodologyTemplateNameClaim.CreationSchema);
        Assert.True(left.ClaimCreation(Root(left, first, leftContent), first, leftContent, Response()).IsSuccess);
        Assert.True(right.ClaimCreation(Root(right, second, rightContent), second, rightContent, Response()).IsSuccess);

        // Act
        await store.Writer.SaveAsync(left, new RequestDispatchContext(RequestActor.System));
        await Assert.ThrowsAsync<EventStreamConcurrencyException>(() => store.Writer.SaveAsync(right,
            new RequestDispatchContext(RequestActor.System)).AsTask());
        var current = await store.Reader.HydrateAsync(new MethodologyTemplateNameClaim(Name, second.CommandId));
        var refused = current.ClaimCreation(Root(current, second, rightContent), second, rightContent, Response());

        // Assert
        Assert.False(refused.IsSuccess);
        Assert.Equal(Target(first.TemplateId), current.Holder);
        Assert.Equal(1UL, current.Version);
    }

    static AggregateScenario<MethodologyTemplateNameClaim> Scenario(Uuid operation) => new(new MethodologyTemplateNameClaim(Name, operation));
    static MethodologyTemplateCreationIntent Intent() => new(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Name, "Original firm purpose", Uuid.CreateVersion4());
    static EventStreamAddress Target(Uuid template) => new(MethodologyTemplate.Realm, MethodologyTemplate.Area, template.ToString());
    static ImmutableContentRef Response() => new(Uuid.CreateVersion4(), new string('a', 64), 2, "application/json", FirstResponses.Schema);
    static OperationProvenance Root(MethodologyTemplateNameClaim owner, MethodologyTemplateCreationIntent intent, ImmutableContentRef content) =>
        new(intent.CommandId, intent.CommandId, null, MethodologyTemplateNameClaim.CreationCommandKind,
            ActorReference.ForPlatformOperator(intent.CreatedBy, intent.CreatedBy.ToString()), owner.Stream,
            intent.TemplateId.ToString(), null, content.Sha256, Now, Target(intent.TemplateId), Target(intent.TemplateId));
    static ClaimedCreation Claimed()
    {
        var intent = Intent();
        var content = Content(intent, MethodologyTemplateNameClaim.CreationSchema);
        var scenario = Scenario(intent.CommandId);
        var operation = Root(scenario.Aggregate, intent, content);
        Assert.True(scenario.Aggregate.ClaimCreation(operation, intent, content, Response()).IsSuccess);
        return new ClaimedCreation(scenario, intent, content, operation,
            Assert.IsType<MethodologyTemplateNameClaimed>(Assert.Single(scenario.PendingEvents)));
    }
    static MethodologyTemplateCreatedV4 Existing(MethodologyTemplateCreationIntent intent, string name)
    {
        var fact = new MethodologyTemplateCreatedV4(intent.TemplateId, name, "Another original purpose", Uuid.CreateVersion4());
        fact.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), intent.TemplateId, 1, Now));
        return fact;
    }
    static Conflict Refusal(ClaimedCreation claim, MethodologyTemplateCreatedV4 existing)
    {
        var proofs = new MethodologyTemplateCreationConflictProofs(Proof(claim.Scenario.Aggregate.Stream, claim.Fact),
            Proof(Target(existing.TemplateId), existing));
        var content = Content(proofs, MethodologyTemplateNameClaim.ConflictSchema);
        var operation = new OperationProvenance(Uuid.CreateVersion4(), claim.Operation.RootOperationId,
            existing.Metadata.EventId, MethodologyTemplateNameClaim.RefusalCommandKind,
            ActorReference.ForSystemProcess(MethodologyTemplateNameClaim.ProcessId, "Methodology template creation"),
            claim.Scenario.Aggregate.Stream, existing.TemplateId.ToString(), null, content.Sha256, Now,
            Target(existing.TemplateId), Target(existing.TemplateId));
        return new Conflict(existing, proofs, content, operation);
    }
    static SourceFactProof<T> Proof<T>(EventStreamAddress stream, T fact) where T : DomainEvent => new(stream,
        fact.Metadata.EventId, fact.Metadata.AggregateVersion - 1, fact.Metadata.AggregateVersion,
        EventKind.Of(fact), ContentDigest.Sha256(fact, typeof(T)),
        new ImmutableContentRef(Uuid.CreateVersion4(), new string('b', 64), 2, "application/json", SourceFactProofs.RequiredFactsSchema),
        100 + fact.Metadata.AggregateVersion, 10 + fact.Metadata.AggregateVersion);
    static ImmutableContentRef Content<T>(T value, string schema) where T : notnull
    {
        var info = ComplianceCoreJsonContext.Default.GetTypeInfo(typeof(T))!;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, info);
        return new ImmutableContentRef(Uuid.CreateVersion4(), ContentDigest.Sha256(bytes), bytes.LongLength, "application/json", schema);
    }
    sealed record ClaimedCreation(AggregateScenario<MethodologyTemplateNameClaim> Scenario, MethodologyTemplateCreationIntent Intent,
        ImmutableContentRef Content, OperationProvenance Operation, MethodologyTemplateNameClaimed Fact);
    sealed record Conflict(MethodologyTemplateCreatedV4 Existing, MethodologyTemplateCreationConflictProofs Proofs,
        ImmutableContentRef Content, OperationProvenance Operation);
}
