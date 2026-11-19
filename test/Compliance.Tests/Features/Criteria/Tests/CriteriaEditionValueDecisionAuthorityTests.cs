using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Services;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Models;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaEditionValueDecisionAuthorityTests
{
    [Theory]
    [InlineData(7UL, true)]
    [InlineData(11UL, false)]
    public async Task ShouldDecideByExactSourceOrderGivenAQualifiedPublishedCut(ulong proposalArea, bool accepted)
    {
        // Arrange
        var fixture = new Fixture(proposalArea, terminal: "publication", covered: true);

        // Act
        var result = await fixture.DecideAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(accepted, result.Accepted);
        Assert.Equal(accepted ? null : "edition_published", result.Reason);
        Assert.Equal("publication", result.Evidence.Basis);
        Assert.Equal(4UL, result.Evidence.EditionResourceOffset);
        Assert.Equal(9UL, result.Evidence.EditionAreaOffset);
    }

    [Fact]
    public async Task ShouldAcceptGivenQualifiedCurrentDraftAndExactBinding()
    {
        // Arrange
        var fixture = new Fixture(7, terminal: null, covered: true);

        // Act
        var result = await fixture.DecideAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Accepted);
        Assert.Equal("draft", result.Evidence.Basis);
        Assert.Equal(5UL, result.Evidence.EditionAreaOffset);
        Assert.Equal(7UL, result.Evidence.ProposalAreaOffset);
    }

    [Fact]
    public async Task ShouldRemainPendingGivenPublicationHasStartedWithoutATerminalCut()
    {
        // Arrange
        var fixture = new Fixture(7, terminal: null, covered: true);
        fixture.MarkPublishing();

        // Act
        var result = await fixture.DecideAsync();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ShouldRemainPendingGivenAnUnadmittedIntermediateRename()
    {
        // Arrange
        var fixture = new Fixture(7, terminal: null, covered: true);
        fixture.MarkInvalidRename();

        // Act
        var result = await fixture.DecideAsync();

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task ShouldRemainPendingGivenUnqualifiedSourceOrHead(bool covered, bool fresh)
    {
        // Arrange
        var fixture = new Fixture(7, terminal: null, covered: covered) { Fresh = fresh };

        // Act
        var result = await fixture.DecideAsync();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ShouldRemainPendingGivenUnobservedOperationClaim()
    {
        // Arrange
        var fixture = new Fixture(7, "publication", true) { ShowClaim = false };

        // Act
        var result = await fixture.DecideAsync();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ShouldRemainPendingGivenUnqualifiedCrossAreaCausality()
    {
        // Arrange
        var fixture = new Fixture(7, terminal: null, covered: true) { Causal = false };

        // Act
        var result = await fixture.DecideAsync();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ShouldRejectClaimGivenItWasOrderedAfterTheProposal()
    {
        // Arrange
        var fixture = new Fixture(7, "publication", true) { ClaimArea = 8 };

        // Act
        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await fixture.DecideAsync());
    }

    [Fact]
    public async Task ShouldRefuseGivenAQualifiedAbandonment()
    {
        // Arrange
        var fixture = new Fixture(7, "abandonment", true);

        // Act
        var result = await fixture.DecideAsync();

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Accepted);
        Assert.Equal("edition_abandoned", result.Reason);
        Assert.Equal("abandonment", result.Evidence.Basis);
    }

    [Theory]
    [InlineData("publication", 7UL, 1)]
    [InlineData("publication", 11UL, 0)]
    [InlineData("abandonment", 7UL, 2)]
    public async Task ShouldRevalidateDraftDecisionGivenAnInterveningTerminalExists(string terminal, ulong proposalArea,
        int expected)
    {
        // Arrange
        var fixture = new Fixture(proposalArea, terminal, covered: true);
        var outcome = fixture.RecordDraftOutcome(12);

        // Act
        var result = await fixture.VerifyAsync(outcome);

        // Assert
        Assert.Equal((CriteriaEntryDecisionVerification)expected, result);
    }

    [Fact]
    public async Task ShouldKeepDraftDecisionPendingGivenUnqualifiedCoverageThroughOutcome()
    {
        // Arrange
        var fixture = new Fixture(7, terminal: null, covered: false);
        var outcome = fixture.RecordDraftOutcome(8);

        // Act
        var result = await fixture.VerifyAsync(outcome);

        // Assert
        Assert.Equal(CriteriaEntryDecisionVerification.Pending, result);
    }

    [Fact]
    public async Task ShouldRejectDraftEvidenceGivenItNamesThePublishingHead()
    {
        // Arrange
        var fixture = new Fixture(7, terminal: null, covered: true);
        fixture.MarkPublishing();
        var outcome = fixture.RecordDraftOutcome(10, usePublishingHead: true);

        // Act
        var result = await fixture.VerifyAsync(outcome);

        // Assert
        Assert.Equal(CriteriaEntryDecisionVerification.Pending, result);
    }

    sealed class Fixture : ICriteriaExactSourceReader, ICriteriaSourceCutCoverage, ICriteriaPublicationAuthority,
        ICriteriaFreshEntryHead, ICriteriaParticipantCausalOrder
    {
        readonly List<CriteriaOrderedSourceEvent> _source = [];
        readonly CriteriaOrderedSourceEvent _claim;
        readonly CriteriaOrderedSourceEvent _drafted;
        CriteriaOrderedSourceEvent? _publishing;
        readonly CriteriaOrderedSourceEvent? _terminal;
        readonly bool _covered;

        public Fixture(ulong proposalArea, string? terminal, bool covered)
        {
            _covered = covered;
            var edition = Uuid.CreateVersion4();
            var draft = Uuid.CreateVersion4();
            var operation = Uuid.CreateVersion4();
            var actor = new ActorAttribution("operator", "bdgrz");
            var owner = new CriteriaEditionValue(edition, "field", "coverage_note", null, null, operation);
            var claimOwner = new CriteriaEditionValueOperation(edition, "field", "coverage_note", null, null, operation);
            var editionOwner = new CriteriaEdition(edition);
            var labelOwner = new EditionLabelClaim("edition_1");
            var intent = new CriteriaEditionValueIntent(edition, draft, "field", "coverage_note", null, null, operation, 0, "Partial.", null);
            var source = new AccessAdmissionSourceVersion("stream://bdgrz/authority/source", "v1", 1);
            var user = Uuid.CreateVersion4();
            AccessAdmissionEvidence Evidence(Uuid op, string target, string digest) => new(user, actor.Subject, actor.Issuer,
                op, target, CriteriaEdition.BindAdmissionDigest(digest, 1),
                new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero), "policy", source, source)
            { Operator = source };
            var valueAdmission = Evidence(operation, owner.Stream.ToString(), AuthoredIntentDigest.Compute(intent));
            var draftCommand = new DraftCriteriaEdition(edition, draft, "tsc", "edition_1");
            var request = DomainEventSeed.Attach(new CriteriaEditionDraftRequested(edition, draft, draft, 1, "tsc", "edition_1", actor,
                Evidence(draft, editionOwner.Stream.ToString(), AuthoredIntentDigest.Compute(draftCommand)), null, null), editionOwner.Id, 1);
            var reservation = DomainEventSeed.Attach(new EditionLabelReserved("edition_1", edition, draft, 1,
                request.Metadata.EventId, actor), labelOwner.Id, 1);
            var activation = DomainEventSeed.Attach(new CriteriaEditionDraftActivated(edition, draft, draft, "tsc", "edition_1", 1,
                reservation.Metadata.EventId), editionOwner.Id, 2);
            var confirmation = DomainEventSeed.Attach(new EditionLabelConfirmed("edition_1", edition, draft, 1,
                activation.Metadata.EventId), labelOwner.Id, 2);
            var drafted = DomainEventSeed.Attach(new CriteriaEditionDrafted(edition, draft, draft, "tsc", "edition_1", 1,
                confirmation.Metadata.EventId), editionOwner.Id, 3);
            var claim = DomainEventSeed.Attach(new CriteriaEditionValueOperationClaimed(intent, 1, actor, valueAdmission), claimOwner.Id, 1);
            var proposal = DomainEventSeed.Attach(new CriteriaEditionValueProposed(intent, 1, request.Metadata.EventId,
                claim.Metadata.EventId, actor, valueAdmission), owner.Id, 1);
            var attempt = Uuid.CreateVersion4();
            var witnessIncarnation = Uuid.CreateVersion4();
            var begin = new BeginCriteriaPublication(edition, attempt, "CC1.1", witnessIncarnation);
            var started = DomainEventSeed.Attach(new CriteriaPublicationStarted(edition, draft, attempt, 1, 1, "CC1.1",
                witnessIncarnation, "edition_1", 1, confirmation.Metadata.EventId, actor,
                Evidence(attempt, editionOwner.Stream.ToString(), AuthoredIntentDigest.Compute(begin))), editionOwner.Id, 4);
            var published = DomainEventSeed.Attach(new CriteriaEditionPublished(edition, draft, attempt, 1,
                "CC1.1", witnessIncarnation, "edition_1", 1, Uuid.CreateVersion4(), confirmation.Metadata.EventId, actor), editionOwner.Id, 5);
            var abandonId = Uuid.CreateVersion4();
            var abandon = DomainEventSeed.Attach(new CriteriaEditionDraftAbandoned(edition, draft, abandonId, 1, "edition_1", 1, actor,
                Evidence(abandonId, editionOwner.Stream.ToString(), AuthoredIntentDigest.Compute(new AbandonCriteriaEditionDraft(edition, abandonId)))), editionOwner.Id, 4);
            _source.Add(new CriteriaOrderedSourceEvent(editionOwner.Stream, 0, 1, request));
            _source.Add(new CriteriaOrderedSourceEvent(labelOwner.Stream, 0, 2, reservation));
            _source.Add(new CriteriaOrderedSourceEvent(editionOwner.Stream, 1, 3, activation));
            _source.Add(new CriteriaOrderedSourceEvent(labelOwner.Stream, 1, 4, confirmation));
            _drafted = new CriteriaOrderedSourceEvent(editionOwner.Stream, 2, 5, drafted);
            _source.Add(_drafted);
            _claim = new CriteriaOrderedSourceEvent(claimOwner.Stream, 0, 6, claim);
            Proposal = new CriteriaOrderedSourceEvent(owner.Stream, 0, proposalArea, proposal);
            _source.Add(Proposal);
            if (terminal == "publication")
                _source.Add(new CriteriaOrderedSourceEvent(editionOwner.Stream, 3, 8, started));
            _terminal = terminal is null ? null : new CriteriaOrderedSourceEvent(editionOwner.Stream,
                terminal == "abandonment" ? 3UL : 4UL, 9, terminal == "abandonment" ? abandon : published);
            if (_terminal is not null)
                _source.Add(_terminal);
        }

        public bool ShowClaim { get; init; } = true;
        public bool Fresh { get; init; } = true;
        public bool Causal { get; init; } = true;
        public ulong ClaimArea { get; init; } = 6;
        public CriteriaOrderedSourceEvent Proposal { get; }
        public void MarkPublishing()
        {
            var drafted = (CriteriaEditionDrafted)_drafted.Event;
            var actor = new ActorAttribution("operator", "bdgrz");
            var attempt = Uuid.CreateVersion4();
            var incarnation = Uuid.CreateVersion4();
            var digest = AuthoredIntentDigest.Compute(new BeginCriteriaPublication(drafted.EditionId, attempt, "CC1.1", incarnation));
            var source = new AccessAdmissionSourceVersion("stream://bdgrz/authority/source", "v1", 1);
            var admission = new AccessAdmissionEvidence(Uuid.CreateVersion4(), actor.Subject, actor.Issuer, attempt,
                _drafted.Stream.ToString(), CriteriaEdition.BindAdmissionDigest(digest, 1),
                new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero), "policy", source, source)
            {
                FormatVersion = 1,
                PolicyContentSha256 = new string('a', 64),
                Operator = source
            };
            var fact = DomainEventSeed.Attach(new CriteriaPublicationStarted(drafted.EditionId, drafted.DraftGeneration,
                attempt, 1, 1, "CC1.1", incarnation, drafted.Label, drafted.LabelGeneration,
                drafted.ConfirmationEventId, actor, admission),
                drafted.Metadata.AggregateId, 4);
            _publishing = new CriteriaOrderedSourceEvent(_drafted.Stream, 3, 8, fact);
            _source.Add(_publishing);
        }

        public void MarkInvalidRename()
        {
            var drafted = (CriteriaEditionDrafted)_drafted.Event;
            var fact = DomainEventSeed.Attach(new CriteriaEditionRenameRequested(drafted.EditionId, Uuid.CreateVersion4(),
                1, drafted.Label, drafted.LabelGeneration, "edition_2", new ActorAttribution("operator", "bdgrz"), null!),
                drafted.Metadata.AggregateId, 4);
            _source.Add(new CriteriaOrderedSourceEvent(_drafted.Stream, 3, 8, fact));
        }
        public CriteriaOrderedSourceEvent RecordDraftOutcome(ulong area, bool usePublishingHead = false)
        {
            var proposal = (CriteriaEditionValueProposed)Proposal.Event;
            var head = usePublishingHead ? _publishing! : _drafted;
            var evidence = new CriteriaEditionValueDecisionEvidence("draft", head.Event.Metadata.EventId,
                head.ResourceOffset, head.AreaOffset, Proposal.AreaOffset);
            var fact = DomainEventSeed.Attach(new CriteriaEditionValueAccepted(proposal.Intent.EditionId,
                proposal.Intent.OperationId, proposal.ExecutionGeneration, proposal.Metadata.EventId, evidence),
                proposal.Metadata.AggregateId, 2);
            var record = new CriteriaOrderedSourceEvent(Proposal.Stream, 1, area, fact);
            _source.Add(record);
            return record;
        }

        public ValueTask<CriteriaEntryDecisionVerification> VerifyAsync(CriteriaOrderedSourceEvent outcome) =>
            new CriteriaEditionValueDecisionEvidenceVerifier(this, this, this, this, this).VerifyAsync(Proposal, outcome, CancellationToken.None);
        public ValueTask<CriteriaEditionValueDecision?> DecideAsync() =>
            new CriteriaEditionValueDecisionAuthority(this, this, this, this, this).DecideAsync(Proposal, CancellationToken.None);
        public ValueTask<bool> CoversAsync(EventStreamAddress stream, ulong nextResourceOffset, ulong cutAreaOffset,
            CancellationToken ct = default) => ValueTask.FromResult(_covered);
        public ValueTask<bool> IsCurrentAsync(EventStreamAddress stream, Uuid aggregateId, ulong nextResourceOffset,
            CancellationToken ct = default) => ValueTask.FromResult(Fresh);
        public ValueTask<bool> PrecedesAsync(CriteriaOrderedSourceEvent first, CriteriaOrderedSourceEvent second,
            CancellationToken ct = default) => ValueTask.FromResult(Causal);
        public ValueTask<Result<CriteriaPublicationProof>> ReadPublishedAsync(Uuid editionId, CancellationToken ct = default) =>
            ValueTask.FromResult(Result<CriteriaPublicationProof>.Success(new CriteriaPublicationProof(editionId,
                _terminal!.Event.Metadata.EventId, _terminal.ResourceOffset)));
        public ValueTask<CriteriaOrderedSourceEvent?> FindAsync(EventStreamAddress stream, Uuid aggregateId,
            Uuid eventId, CancellationToken ct = default)
        {
            if (ShowClaim && _claim.Stream == stream && _claim.Event.Metadata.AggregateId == aggregateId && _claim.Event.Metadata.EventId == eventId)
                return ValueTask.FromResult<CriteriaOrderedSourceEvent?>(_claim with { AreaOffset = ClaimArea });
            return ValueTask.FromResult(_source.FirstOrDefault(x => x.Stream == stream &&
                x.Event.Metadata.AggregateId == aggregateId && x.Event.Metadata.EventId == eventId));
        }
        public async IAsyncEnumerable<CriteriaOrderedSourceEvent> ReadAsync(EventStreamAddress stream, Uuid aggregateId,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var item in _source.Where(x => x.Stream == stream && x.Event.Metadata.AggregateId == aggregateId))
            {
                ct.ThrowIfCancellationRequested();
                yield return item;
            }
            await Task.CompletedTask;
        }
    }
}
