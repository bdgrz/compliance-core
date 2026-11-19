using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Kernel.Services;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaEditionValueTests
{
    static readonly Uuid TerminalId = Uuid.CreateVersion4();
    static readonly ActorAttribution Actor = new("operator", "issuer");
    static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldUseSeparateOwnersGivenDifferentFieldsAndDisclosures()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();

        // Act
        var note = CriteriaEditionValue.IdentityOf(edition, "field", "coverage_note", null, null);
        var url = CriteriaEditionValue.IdentityOf(edition, "field", "source_url", null, null);
        var gap = CriteriaEditionValue.IdentityOf(edition, "disclosure", null, "privacy", "lifecycle");
        var otherGap = CriteriaEditionValue.IdentityOf(edition, "disclosure", null, "privacy", "other");

        // Assert
        Assert.Equal(4, new[] { note, url, gap, otherGap }.Distinct().Count());
    }

    [Theory]
    [InlineData("coverage_note", "Partial points of focus.", null)]
    [InlineData("source_url", "https://example.test/criteria", null)]
    [InlineData("content_rights", "Original summaries only.", null)]
    [InlineData("numbered_criteria_complete", null, true)]
    public void ShouldAcceptOneBoundedFieldGivenAnExactProposal(string field, string? text, bool? value)
    {
        // Arrange
        var fixture = new Fixture("field", field, null, null, text, value);
        var scenario = new AggregateScenario<CriteriaEditionValue>(fixture.Target);

        // Act
        var result = fixture.Propose();
        var proposal = Assert.IsType<CriteriaEditionValueProposed>(Assert.Single(scenario.PendingEvents));
        var decision = fixture.Target.Decide(fixture.Intent.OperationId, 1, proposal.Metadata.EventId, true, null, new CriteriaEditionValueDecisionEvidence("publication", TerminalId, 3, 10, 5));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(decision.IsSuccess);
        Assert.Equal(text, fixture.Target.TextValue);
        Assert.Equal(value, fixture.Target.BooleanValue);
        Assert.Equal(1, fixture.Target.Revision);
        Assert.Equal("accepted", fixture.Target.Receipt(fixture.Intent.OperationId)!.Status);
    }

    [Fact]
    public void ShouldRejectOversizedContentRightsGivenAnOperationClaim()
    {
        // Arrange
        var fixture = new Fixture("field", "content_rights", null, null, "Original summaries only.", null);
        var intent = fixture.Intent with { OperationId = Uuid.CreateVersion4(), TextValue = new string('x', 2001) };
        var owner = new CriteriaEditionValueOperation(intent.EditionId, "field", "content_rights", null, null,
            intent.OperationId);
        var target = new CriteriaEditionValue(intent.EditionId, "field", "content_rights", null, null, intent.OperationId);
        var admission = Fixture.AdmissionFor(target, intent, Uuid.CreateVersion4());

        // Act
        var result = owner.Claim(intent, 1, Actor, admission);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(new AggregateScenario<CriteriaEditionValueOperation>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldReturnSameResultGivenExactRetryAndRefuseChangedIntent()
    {
        // Arrange
        var fixture = new Fixture("disclosure", null, "privacy", "lifecycle", "Not supported.", null);
        Assert.True(fixture.Propose().IsSuccess);
        var scenario = new AggregateScenario<CriteriaEditionValue>(fixture.Target);
        var changed = fixture.Intent with { TextValue = "Different." };

        // Act
        var exact = fixture.Propose();
        var conflict = fixture.Target.Propose(changed, 1, Actor, fixture.Admission, fixture.Request, fixture.Claim);

        // Assert
        Assert.True(exact.IsSuccess);
        Assert.False(conflict.IsSuccess);
        Assert.Single(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldLeaveValueUnchangedGivenARefusedProposal()
    {
        // Arrange
        var fixture = new Fixture("disclosure", null, "privacy", "lifecycle", "Not supported.", null);
        Assert.True(fixture.Propose().IsSuccess);
        var proposal = Assert.IsType<CriteriaEditionValueProposed>(Assert.Single(new AggregateScenario<CriteriaEditionValue>(fixture.Target).PendingEvents));

        // Act
        var result = fixture.Target.Decide(fixture.Intent.OperationId, 1, proposal.Metadata.EventId, false, "edition_published", new CriteriaEditionValueDecisionEvidence("publication", TerminalId, 3, 10, 15));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, fixture.Target.Revision);
        Assert.Null(fixture.Target.TextValue);
        Assert.Equal("refused", fixture.Target.Receipt(fixture.Intent.OperationId)!.Status);
    }

    [Fact]
    public void ShouldRecoverSelectedReceiptGivenAReplayedAcceptedOutcome()
    {
        // Arrange
        var fixture = new Fixture("field", "coverage_note", null, null, "Partial.", null);
        Assert.True(fixture.Propose().IsSuccess);
        var proposal = Assert.IsType<CriteriaEditionValueProposed>(Assert.Single(new AggregateScenario<CriteriaEditionValue>(fixture.Target).PendingEvents));
        Assert.True(fixture.Target.Decide(fixture.Intent.OperationId, 1, proposal.Metadata.EventId, true, null, new CriteriaEditionValueDecisionEvidence("publication", TerminalId, 3, 10, 5)).IsSuccess);
        var history = new AggregateScenario<CriteriaEditionValue>(fixture.Target).PendingEvents.ToArray();
        var replayed = new CriteriaEditionValue(fixture.Intent.EditionId, "field", "coverage_note", null, null,
            fixture.Intent.OperationId);
        new AggregateScenario<CriteriaEditionValue>(replayed).Given(history);

        // Act
        var retry = replayed.Decide(fixture.Intent.OperationId, 1, proposal.Metadata.EventId, true, null, new CriteriaEditionValueDecisionEvidence("publication", TerminalId, 3, 10, 5));
        var differentCut = replayed.Decide(fixture.Intent.OperationId, 1, proposal.Metadata.EventId, true, null, new CriteriaEditionValueDecisionEvidence("publication", TerminalId, 3, 11, 5));

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.False(differentCut.IsSuccess);
        Assert.Equal("Partial.", replayed.TextValue);
        Assert.Equal("accepted", replayed.Receipt(fixture.Intent.OperationId)!.Status);
    }

    [Fact]
    public void ShouldCorrectOneValueGivenAnAcceptedEarlierRevision()
    {
        // Arrange
        var fixture = new Fixture("field", "coverage_note", null, null, "Partial.", null);
        Assert.True(fixture.Propose().IsSuccess);
        var first = Assert.IsType<CriteriaEditionValueProposed>(Assert.Single(new AggregateScenario<CriteriaEditionValue>(fixture.Target).PendingEvents));
        Assert.True(fixture.Target.Decide(fixture.Intent.OperationId, 1, first.Metadata.EventId, true, null, new CriteriaEditionValueDecisionEvidence("publication", TerminalId, 3, 10, 5)).IsSuccess);
        var history = new AggregateScenario<CriteriaEditionValue>(fixture.Target).PendingEvents.ToArray();
        var correction = fixture.Intent with { OperationId = Uuid.CreateVersion4(), ExpectedRevision = 1, TextValue = "Updated." };
        var owner = new CriteriaEditionValue(correction.EditionId, "field", "coverage_note", null, null, correction.OperationId);
        new AggregateScenario<CriteriaEditionValue>(owner).Given(history);
        var admission = Fixture.AdmissionFor(owner, correction, fixture.Admission.UserId);
        var claimOwner = new CriteriaEditionValueOperation(correction.EditionId, "field", "coverage_note", null, null, correction.OperationId);
        Assert.True(claimOwner.Claim(correction, 1, Actor, admission).IsSuccess);
        var claim = Assert.IsType<CriteriaEditionValueOperationClaimed>(Assert.Single(new AggregateScenario<CriteriaEditionValueOperation>(claimOwner).PendingEvents));

        // Act
        var proposed = owner.Propose(correction, 1, Actor, admission, fixture.Request, claim);
        var fact = Assert.IsType<CriteriaEditionValueProposed>(Assert.Single(new AggregateScenario<CriteriaEditionValue>(owner).PendingEvents));
        var decided = owner.Decide(correction.OperationId, 1, fact.Metadata.EventId, true, null, new CriteriaEditionValueDecisionEvidence("publication", TerminalId, 3, 10, 5));

        // Assert
        Assert.True(proposed.IsSuccess);
        Assert.True(decided.IsSuccess);
        Assert.Equal(2, owner.Revision);
        Assert.Equal("Updated.", owner.TextValue);
    }

    [Fact]
    public void ShouldRejectForeignSourceOrOversizedValueWithoutFact()
    {
        // Arrange
        var fixture = new Fixture("field", "coverage_note", null, null, "Partial.", null);
        var scenario = new AggregateScenario<CriteriaEditionValue>(fixture.Target);

        // Act
        var wrongSource = fixture.Target.Propose(fixture.Intent, 1, Actor, fixture.Admission,
            fixture.Request with { Framework = "other" }, fixture.Claim);
        var large = fixture.Intent with { TextValue = new string('x', 2001) };
        var oversized = fixture.Target.Propose(large, 1, Actor, fixture.Admission, fixture.Request, fixture.Claim);

        // Assert
        Assert.False(wrongSource.IsSuccess);
        Assert.False(oversized.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRejectChangedIntentGivenAnExistingOperationClaim()
    {
        // Arrange
        var fixture = new Fixture("field", "coverage_note", null, null, "Partial.", null);
        var owner = new CriteriaEditionValueOperation(fixture.Intent.EditionId, "field", "coverage_note", null,
            null, fixture.Intent.OperationId);
        new AggregateScenario<CriteriaEditionValueOperation>(owner).Given(fixture.Claim);
        var changed = fixture.Intent with { TextValue = "Different." };
        var admission = Fixture.AdmissionFor(fixture.Target, changed, fixture.Admission.UserId);
        var scenario = new AggregateScenario<CriteriaEditionValueOperation>(owner);

        // Act
        var result = owner.Claim(changed, 1, Actor, admission);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public async Task ShouldAllowOnlyOneAppendGivenCompetingInitialTargetProposals()
    {
        // Arrange
        var fixture = new Fixture("field", "coverage_note", null, null, "Partial.", null);
        var other = new CriteriaEditionValue(fixture.Intent.EditionId, "field", "coverage_note", null, null,
            fixture.Intent.OperationId);
        Assert.True(fixture.Propose().IsSuccess);
        Assert.True(other.Propose(fixture.Intent, 1, Actor, fixture.Admission, fixture.Request, fixture.Claim).IsSuccess);
        var firstFacts = new AggregateScenario<CriteriaEditionValue>(fixture.Target).PendingEvents.ToArray();
        var competingFacts = new AggregateScenario<CriteriaEditionValue>(other).PendingEvents.ToArray();
        var store = new InMemoryEventStore();

        // Act
        await store.AppendAsync(fixture.Target.Stream, 0, firstFacts, CancellationToken.None);
        var failure = await Xunit.Record.ExceptionAsync(async () =>
            await store.AppendAsync(other.Stream, 0, competingFacts, CancellationToken.None));

        // Assert
        Assert.IsType<EventStreamConcurrencyException>(failure);
    }

    sealed class Fixture
    {
        public Fixture(string kind, string? field, string? category, string? code, string? text, bool? boolean)
        {
            var edition = Uuid.CreateVersion4();
            var draft = Uuid.CreateVersion4();
            var operation = Uuid.CreateVersion4();
            Intent = new CriteriaEditionValueIntent(edition, draft, kind, field, category, code, operation, 0, text, boolean);
            Target = new CriteriaEditionValue(edition, kind, field, category, code, operation);
            Admission = AdmissionFor(Target, Intent, Uuid.CreateVersion4());
            Request = DomainEventSeed.Attach(new CriteriaEditionDraftRequested(edition, draft, draft, 1,
                "tsc", "edition_1", Actor, Admission, null, null), new CriteriaEdition(edition).Id, 1);
            var owner = new CriteriaEditionValueOperation(edition, kind, field, category, code, operation);
            Assert.True(owner.Claim(Intent, 1, Actor, Admission).IsSuccess);
            Claim = Assert.IsType<CriteriaEditionValueOperationClaimed>(Assert.Single(new AggregateScenario<CriteriaEditionValueOperation>(owner).PendingEvents));
        }

        public CriteriaEditionValueIntent Intent { get; }
        public CriteriaEditionValue Target { get; }
        public AccessAdmissionEvidence Admission { get; }
        public CriteriaEditionDraftRequested Request { get; }
        public CriteriaEditionValueOperationClaimed Claim { get; }
        public Result Propose() => Target.Propose(Intent, 1, Actor, Admission, Request, Claim);

        public static AccessAdmissionEvidence AdmissionFor(CriteriaEditionValue target, CriteriaEditionValueIntent intent, Uuid userId)
        {
            var identity = new AccessAdmissionSourceVersion("stream://bdgrz/identities/actor", "generation", 1);
            var policy = new AccessAdmissionSourceVersion("stream://bdgrz/policies/current", "generation", 1);
            return new AccessAdmissionEvidence(userId, Actor.Subject, Actor.Issuer, intent.OperationId,
                target.Stream.ToString(), CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(intent), 1),
                Now, "policy-v1", identity, policy)
            {
                FormatVersion = 1,
                PolicyContentSha256 = new string('a', 64),
                Operator = new AccessAdmissionSourceVersion("stream://bdgrz/operators/actor", "generation", 1),
            };
        }
    }
}
