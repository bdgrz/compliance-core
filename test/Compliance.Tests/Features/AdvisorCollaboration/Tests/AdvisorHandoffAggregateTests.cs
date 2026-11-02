using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Aggregates;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Events;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;

/// <summary>The handoff owner's own decisions: creation, lifecycle checks and replay of valid history.</summary>
public sealed class AdvisorHandoffAggregateTests
{
    static readonly DateOnly Today = new(2026, 10, 29);
    static readonly ActorReference Lead = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");

    [Fact]
    public void ShouldAppendCreationFactGivenValidTermsAndComparisonBaseline()
    {
        // Arrange
        var owner = NewOwner(out var handoffId);
        var baseline = Uuid.CreateVersion4();

        // Act
        var result = Create(owner, handoffId, baseline);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var fact = Assert.IsType<AdvisorHandoffCreated>(Assert.Single(Facts(owner)));
        Assert.Equal(handoffId, fact.HandoffId);
        Assert.Equal(baseline, fact.ComparedToHandoffId);
        Assert.Equal(Today.AddDays(30), fact.ExpiresOn);
        Assert.Equal(Lead, fact.CreatedBy);
        Assert.Equal(AdvisorHandoff.Draft, owner.Status);
        Assert.Equal(1, owner.Revision);
        Assert.Equal(1, owner.Generation);
    }

    [Fact]
    public void ShouldRefuseCreationAndAppendNothingGivenComparisonToItself()
    {
        // Arrange
        var owner = NewOwner(out var handoffId);

        // Act
        var result = Create(owner, handoffId, handoffId);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(Facts(owner));
        Assert.False(owner.IsCreated);
    }

    [Fact]
    public void ShouldRefuseCreationAndAppendNothingGivenEmptyComparisonBaseline()
    {
        // Arrange
        var owner = NewOwner(out var handoffId);

        // Act
        var result = Create(owner, handoffId, Uuid.Empty);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(Facts(owner));
    }

    [Fact]
    public void ShouldRefuseCreationAndAppendNothingGivenOperationOfAnotherLogicalId()
    {
        // Arrange
        var owner = NewOwner(out _);
        var foreign = NativeFacts.Root(owner, Uuid.CreateVersion4(), "CreateAdvisorHandoff", null);

        // Act
        var result = owner.Create(foreign, NativeFacts.Ref(), NativeFacts.Ref(), Today.AddDays(30), Lead,
            NativeFacts.Now, null, NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(Facts(owner));
    }

    [Fact]
    public void ShouldRefuseSecondCreationGivenHandoffAlreadyCreated()
    {
        // Arrange
        var owner = NewOwner(out var handoffId);
        Assert.True(Create(owner, handoffId, null).IsSuccess);

        // Act
        var result = Create(owner, handoffId, null);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Single(Facts(owner));
    }

    [Fact]
    public void ShouldReportNotFoundGivenHandoffNeverCreated()
    {
        // Arrange
        var owner = NewOwner(out _);

        // Act
        var results = new[]
        {
            owner.PrepareRevise(0), owner.PrepareReady(0, Today), owner.PrepareEnd(0), owner.PrepareAdmit(0, Today),
        };

        // Assert
        Assert.All(results, result => Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind));
    }

    [Fact]
    public void ShouldRefuseGivenStaleExpectedRevisionOnDraftHandoff()
    {
        // Arrange
        var owner = Replay();

        // Act
        var results = new[] { owner.PrepareRevise(0), owner.PrepareReady(2, Today), owner.PrepareEnd(7) };

        // Assert
        Assert.All(results, result => Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind));
        Assert.True(owner.PrepareRevise(1).IsSuccess);
        Assert.True(owner.PrepareReady(1, Today).IsSuccess);
        Assert.True(owner.PrepareEnd(1).IsSuccess);
    }

    [Fact]
    public void ShouldRefuseReadyGivenExpiryPassed()
    {
        // Arrange
        var owner = Replay();

        // Act
        var result = owner.PrepareReady(1, Today.AddDays(31));

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
    }

    [Fact]
    public void ShouldRefuseSecondReadinessGivenHandoffAlreadyReady()
    {
        // Arrange
        var owner = Replay("ready");

        // Act
        var result = owner.PrepareReady(2, Today);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(AdvisorHandoff.ReadyForReview, owner.Status);
    }

    [Fact]
    public void ShouldAdvanceGenerationAndReturnToDraftGivenSelectionRevisedAfterReadiness()
    {
        // Arrange
        var owner = Replay("ready", "revise");

        // Act
        var admit = owner.PrepareAdmit(3, Today);

        // Assert
        Assert.Equal(AdvisorHandoff.Draft, owner.Status);
        Assert.Equal(2, owner.Generation);
        Assert.Equal(3, owner.Revision);
        Assert.True(owner.WasReadyForReview);
        Assert.Equal(RequestErrorKind.Conflict, admit.Error?.Kind);
    }

    [Fact]
    public void ShouldAdmitRepeatedlyGivenReadyHandoffAndCurrentRevision()
    {
        // Arrange
        var owner = Replay("ready", "admit", "admit");

        // Act
        var next = owner.PrepareAdmit(4, Today);
        var stale = owner.PrepareAdmit(3, Today);

        // Assert
        Assert.Equal(2, owner.AdmissionCount);
        Assert.Equal(4, owner.Revision);
        Assert.Equal(1, owner.Generation);
        Assert.Equal(AdvisorHandoff.ReadyForReview, owner.Status);
        Assert.True(next.IsSuccess, next.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
    }

    [Fact]
    public void ShouldRefuseAdmissionGivenDraftOrExpiredHandoff()
    {
        // Arrange
        var draft = Replay();
        var ready = Replay("ready");

        // Act
        var notReady = draft.PrepareAdmit(1, Today);
        var expired = ready.PrepareAdmit(2, Today.AddDays(31));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, notReady.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, expired.Error?.Kind);
    }

    [Theory]
    [InlineData("close", AdvisorHandoff.Closed)]
    [InlineData("revoke", AdvisorHandoff.Revoked)]
    public void ShouldRefuseEveryChangeGivenEndedHandoff(string end, string status)
    {
        // Arrange
        var owner = Replay("ready", end);

        // Act
        var results = new[]
        {
            owner.PrepareRevise(3), owner.PrepareReady(3, Today), owner.PrepareEnd(3), owner.PrepareAdmit(3, Today),
        };

        // Assert
        Assert.Equal(status, owner.Status);
        Assert.False(owner.IsOpen);
        Assert.Equal(3, owner.Revision);
        Assert.All(results, result => Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind));
    }

    [Fact]
    public void ShouldRetainCreationTermsGivenReplayAfterLaterFacts()
    {
        // Arrange
        var owner = Replay("ready", "admit", "close");

        // Act
        var created = owner.CreatedFact;

        // Assert
        Assert.NotNull(created);
        Assert.Equal(Today.AddDays(30), owner.ExpiresOn);
        Assert.Equal(created!.CreatedAt, owner.CreatedAt);
        Assert.Equal(Lead, owner.CreatedBy);
        Assert.Equal(1, owner.AdmissionCount);
        Assert.Equal(4, owner.Revision);
    }

    static AdvisorHandoff NewOwner(out Uuid handoffId)
    {
        handoffId = Uuid.CreateVersion4();
        return new AdvisorHandoff(Uuid.CreateVersion4(), Uuid.CreateVersion4(), handoffId);
    }

    static Result Create(AdvisorHandoff owner, Uuid handoffId, Uuid? comparedTo) => owner.Create(
        NativeFacts.Root(owner, handoffId, "CreateAdvisorHandoff", null), NativeFacts.Ref(), NativeFacts.Ref(),
        Today.AddDays(30), Lead, NativeFacts.Now, comparedTo, NativeFacts.Ref(), NativeFacts.Ref());

    static DomainEvent[] Facts(AdvisorHandoff owner) =>
        new AggregateScenario<AdvisorHandoff>(owner).PendingEvents.ToArray();

    /// <summary>A handoff replayed from a valid created fact followed by the named steps.</summary>
    static AdvisorHandoff Replay(params string[] steps)
    {
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var handoffId = Uuid.CreateVersion4();
        var shell = new AdvisorHandoff(tenantId, programId, handoffId);
        var facts = new List<DomainEvent>();
        var revision = 1L;
        var generation = 1L;
        facts.Add(DomainEventSeed.Attach(new AdvisorHandoffCreated(tenantId, programId, handoffId, NativeFacts.Ref(),
            NativeFacts.Ref(), Today.AddDays(30), Lead, NativeFacts.Now, null,
            NativeFacts.Root(shell, handoffId, "CreateAdvisorHandoff", null), NativeFacts.Ref(), NativeFacts.Ref()),
            shell.Id, 1));
        foreach (var step in steps)
        {
            revision++;
            var operation = NativeFacts.Root(shell, Uuid.CreateVersion4(), step, revision - 1);
            DomainEvent fact = step switch
            {
                "ready" => new AdvisorHandoffReadiedForReview(tenantId, programId, handoffId, revision, Lead,
                    NativeFacts.Now, operation, NativeFacts.Ref(), NativeFacts.Ref()),
                "revise" => new AdvisorHandoffSelectionRevised(tenantId, programId, handoffId, revision,
                    NativeFacts.Ref(), NativeFacts.Ref(), Lead, NativeFacts.Now, operation, NativeFacts.Ref(),
                    NativeFacts.Ref()),
                "close" => new AdvisorHandoffClosed(tenantId, programId, handoffId, revision, NativeFacts.Ref(), Lead,
                    NativeFacts.Now, operation, NativeFacts.Ref(), NativeFacts.Ref()),
                "revoke" => new AdvisorHandoffRevoked(tenantId, programId, handoffId, revision, NativeFacts.Ref(), Lead,
                    NativeFacts.Now, operation, NativeFacts.Ref(), NativeFacts.Ref()),
                _ => new AdvisorFeedbackAdmitted(shell.Stream, revision, generation, NativeFacts.Ref(), null,
                    operation, NativeFacts.Ref(), null),
            };
            if (step == "revise")
                generation++;
            facts.Add(DomainEventSeed.Attach(fact, shell.Id, (ulong)facts.Count + 1));
        }

        return NativeFacts.Replay(new AdvisorHandoff(tenantId, programId, handoffId), facts);
    }
}
