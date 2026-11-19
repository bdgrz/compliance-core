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

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class SourceCriteriaEntryAuthorityTests
{
    [Fact]
    public async Task ShouldRejectOversizedIdentifierGivenEntryAuthorityRequest()
    {
        // Arrange
        var fixture = new Fixture(false);

        // Act
        var result = await fixture.Authority.ReadAsync(fixture.EditionId, new string('x', 129));

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldRemainPendingGivenTheResourceReadHasNoCoverageProof()
    {
        // Arrange
        var fixture = new Fixture(false);
        fixture.AddCreate("criterion-1", 20, 30);

        // Act
        var result = await fixture.Authority.ReadAsync(fixture.EditionId, "criterion-1");

        // Assert
        Assert.Equal(CriteriaEntryAuthorityStatus.Pending, result.Value!.Status);
        Assert.Null(result.Value.Proof);
    }

    [Fact]
    public async Task ShouldIncludeProposalGivenItsAcceptedOutcomeFollowsTheCut()
    {
        // Arrange
        var fixture = new Fixture(true);
        fixture.AddCreate("criterion-1", 20, 200);

        // Act
        var result = await fixture.Authority.ReadAsync(fixture.EditionId, "criterion-1");

        // Assert
        Assert.Equal(CriteriaEntryAuthorityStatus.Accepted, result.Value!.Status);
        Assert.Equal("criterion-1", result.Value.Proof!.Entry.Identifier);
        Assert.Equal(fixture.PublicationId, result.Value.Proof.PublicationEventId);
    }

    [Fact]
    public async Task ShouldRemainPendingGivenProposalClaimCannotBeFound()
    {
        // Arrange
        var fixture = new Fixture(true);
        fixture.AddCreate("criterion-1", 20, 200);
        fixture.RemoveClaim("criterion-1");

        // Act
        var result = await fixture.Authority.ReadAsync(fixture.EditionId, "criterion-1");

        // Assert
        Assert.Equal(CriteriaEntryAuthorityStatus.Pending, result.Value!.Status);
    }

    [Fact]
    public async Task ShouldRemainPendingGivenDecisionEditionCoverageIsNotQualified()
    {
        // Arrange
        var fixture = new Fixture(true);
        fixture.AddCreate("criterion-1", 20, 30);
        fixture.WithholdEditionCoverage();

        // Act
        var result = await fixture.Authority.ReadAsync(fixture.EditionId, "criterion-1");

        // Assert
        Assert.Equal(CriteriaEntryAuthorityStatus.Pending, result.Value!.Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRemainPendingGivenMissingOrRejectedParticipantCausality(bool rejected)
    {
        // Arrange
        var fixture = new Fixture(true);
        fixture.AddCreate("criterion-1", 20, 30);

        // Act
        var result = await fixture.ReadWithoutCausalityAsync("criterion-1", rejected);

        // Assert
        Assert.Equal(CriteriaEntryAuthorityStatus.Pending, result.Value!.Status);
        Assert.Null(result.Value.Proof);
    }

    sealed class RejectedCausality : ICriteriaParticipantCausalOrder
    {
        public ValueTask<bool> PrecedesAsync(CriteriaOrderedSourceEvent first, CriteriaOrderedSourceEvent second,
            CancellationToken ct = default) => ValueTask.FromResult(false);
    }

    [Fact]
    public async Task ShouldRemainPendingGivenPreCutProposalHasNoOutcome()
    {
        // Arrange
        var fixture = new Fixture(true);
        fixture.AddCreate("criterion-1", 20, null);

        // Act
        var result = await fixture.Authority.ReadAsync(fixture.EditionId, "criterion-1");

        // Assert
        Assert.Equal(CriteriaEntryAuthorityStatus.Pending, result.Value!.Status);
    }

    [Fact]
    public async Task ShouldExcludeEntryGivenItsOnlyProposalFollowsTheCut()
    {
        // Arrange
        var fixture = new Fixture(true);
        fixture.AddCreate("criterion-1", 120, null);

        // Act
        var result = await fixture.Authority.ReadAsync(fixture.EditionId, "criterion-1");

        // Assert
        Assert.Equal(CriteriaEntryAuthorityStatus.NotMember, result.Value!.Status);
    }

    [Fact]
    public async Task ShouldProveExactParentGivenThePointAndParentAreEffectiveAtTheCut()
    {
        // Arrange
        var fixture = new Fixture(true);
        fixture.AddCreate("parent", 20, 30);
        fixture.AddPoint("focus", "parent", 50, 60);

        // Act
        var result = await fixture.Authority.ReadAsync(fixture.EditionId, "focus");

        // Assert
        Assert.Equal(CriteriaEntryAuthorityStatus.Accepted, result.Value!.Status);
        Assert.NotNull(result.Value.Proof!.ParentProposalEventId);
        Assert.NotNull(result.Value.Proof.ParentDecisionEventId);
    }

    [Fact]
    public async Task ShouldExcludePointGivenItsExactParentWasRemovedBeforeTheCut()
    {
        // Arrange
        var fixture = new Fixture(true);
        fixture.AddCreate("parent", 20, 30);
        fixture.AddPoint("focus", "parent", 50, 60);
        fixture.Remove("parent", 80, 90);

        // Act
        var result = await fixture.Authority.ReadAsync(fixture.EditionId, "focus");

        // Assert
        Assert.Equal(CriteriaEntryAuthorityStatus.NotMember, result.Value!.Status);
    }

    sealed class Fixture : ICriteriaExactSourceReader, ICriteriaPublicationAuthority, ICriteriaSourceCutCoverage
    {
        readonly Dictionary<EventStreamAddress, List<CriteriaOrderedSourceEvent>> _events = [];
        readonly Dictionary<Uuid, CriteriaOrderedSourceEvent> _claims = [];
        readonly bool _covered;
        bool _editionCovered = true;
        readonly Uuid _generation = Uuid.CreateVersion4();
        readonly CriteriaOrderedSourceEvent _publication;
        readonly CriteriaOrderedSourceEvent _draft;
        static readonly ActorAttribution Actor = new("operator", "bdgrz");

        public Fixture(bool covered)
        {
            _covered = covered;
            var edition = new CriteriaEdition(EditionId);
            var source = new AccessAdmissionSourceVersion("stream://bdgrz/authority/source", "v1", 1);
            var user = Uuid.CreateVersion4();
            AccessAdmissionEvidence Admit(Uuid operation, object intent) => new(user, Actor.Subject, Actor.Issuer,
                operation, edition.Stream.ToString(),
                CriteriaEdition.BindAdmissionDigest(intent switch
                {
                    DraftCriteriaEdition draft => AuthoredIntentDigest.Compute(draft),
                    BeginCriteriaPublication start => AuthoredIntentDigest.Compute(start),
                    _ => throw new InvalidOperationException("Unexpected fixture intent."),
                }, 1), DateTimeOffset.UtcNow, "policy", source, source)
            { Operator = source };
            var request = Add(edition.Stream, edition.Id,
                new CriteriaEditionDraftRequested(EditionId, _generation, _generation, 1, "tsc", "edition_1", Actor,
                    Admit(_generation, new DraftCriteriaEdition(EditionId, _generation, "tsc", "edition_1")), null, null), 1);
            var label = new EditionLabelClaim("edition_1");
            var reservation = Add(label.Stream, label.Id, new EditionLabelReserved("edition_1", EditionId, _generation,
                1, request.Event.Metadata.EventId, Actor), 1);
            var activation = Add(edition.Stream, edition.Id, new CriteriaEditionDraftActivated(EditionId, _generation,
                _generation, "tsc", "edition_1", 1, reservation.Event.Metadata.EventId), 2);
            var confirmation = Add(label.Stream, label.Id, new EditionLabelConfirmed("edition_1", EditionId,
                _generation, 1, activation.Event.Metadata.EventId), 2);
            _draft = Add(edition.Stream, edition.Id, new CriteriaEditionDrafted(EditionId, _generation, _generation,
                "tsc", "edition_1", 1, confirmation.Event.Metadata.EventId), 3);
            var attempt = Uuid.CreateVersion4();
            var witness = Uuid.CreateVersion4();
            Add(edition.Stream, edition.Id, new CriteriaPublicationStarted(EditionId, _generation, attempt, 1, 1,
                "witness", witness, "edition_1", 1, confirmation.Event.Metadata.EventId, Actor,
                Admit(attempt, new BeginCriteriaPublication(EditionId, attempt, "witness", witness))), 99);
            _publication = Add(edition.Stream, edition.Id, new CriteriaEditionPublished(EditionId, _generation, attempt, 1,
                "witness", witness, "edition_1", 1, Uuid.CreateVersion4(), confirmation.Event.Metadata.EventId, Actor),
                100, PublicationId);
            Authority = new SourceCriteriaEntryAuthority(this, this, this, CriteriaTestCausalOrder.Qualified);
        }

        CriteriaOrderedSourceEvent Add(EventStreamAddress stream, Uuid ownerId, DomainEvent fact, ulong area, Uuid? eventId = null)
        {
            if (!_events.TryGetValue(stream, out var records))
                _events.Add(stream, records = []);
            var offset = (ulong)records.Count;
            fact.AttachMetadata(new DomainEventMetadata(eventId ?? Uuid.CreateVersion4(), ownerId, offset + 1, DateTimeOffset.UtcNow));
            var record = new CriteriaOrderedSourceEvent(stream, offset, area, fact);
            records.Add(record);
            return record;
        }

        public Uuid EditionId { get; } = Uuid.CreateVersion4();
        public Uuid PublicationId { get; } = Uuid.CreateVersion4();
        public SourceCriteriaEntryAuthority Authority { get; }

        public ValueTask<Result<CriteriaEntryAuthorityResult>> ReadWithoutCausalityAsync(string identifier, bool rejected) =>
            new SourceCriteriaEntryAuthority(this, this, this, rejected ? new RejectedCausality() : null)
                .ReadAsync(EditionId, identifier);

        public void AddCreate(string identifier, ulong proposalArea, ulong? decisionArea)
        {
            var owner = new CriteriaEntry(EditionId, identifier);
            var operation = Uuid.CreateVersion4();
            var intent = new CriterionChangeIntent(EditionId, _generation, identifier, operation, 0, "create",
                "criterion", "security", null, null, "summary");
            var (admission, claim) = Claim(intent, 1, proposalArea - 10);
            var proposal = new CriterionChangeProposed(intent, 1, CriteriaEntry.IncarnationOf(EditionId, identifier, operation),
                Actor, admission, null, claim.Metadata.EventId);
            proposal.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), owner.Id, 1, DateTimeOffset.UtcNow));
            var events = new List<CriteriaOrderedSourceEvent> { new(owner.Stream, 0, proposalArea, proposal) };
            if (decisionArea is { } area)
            {
                var accepted = new CriterionChanged(EditionId, identifier, operation, 1, proposal.Metadata.EventId,
                    Evidence(proposalArea, area));
                accepted.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), owner.Id, 2, DateTimeOffset.UtcNow));
                events.Add(new CriteriaOrderedSourceEvent(owner.Stream, 1, area, accepted));
            }
            _events.Add(owner.Stream, events);
        }

        public void AddPoint(string identifier, string parentId, ulong proposalArea, ulong decisionArea)
        {
            var parent = new CriteriaEntry(EditionId, parentId);
            var parentProposal = (CriterionChangeProposed)_events[parent.Stream][0].Event;
            var parentDecision = (CriterionChanged)_events[parent.Stream][1].Event;
            var owner = new CriteriaEntry(EditionId, identifier);
            var operation = Uuid.CreateVersion4();
            var intent = new CriterionChangeIntent(EditionId, _generation, identifier, operation, 0, "create",
                "point_of_focus", "security", parentId, parentProposal.IncarnationId, "focus summary");
            var (admission, claim) = Claim(intent, 1, proposalArea - 10);
            var proposal = new CriterionChangeProposed(intent, 1, CriteriaEntry.IncarnationOf(EditionId, identifier, operation),
                Actor, admission, parentDecision.Metadata.EventId, claim.Metadata.EventId);
            proposal.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), owner.Id, 1, DateTimeOffset.UtcNow));
            var accepted = new CriterionChanged(EditionId, identifier, operation, 1, proposal.Metadata.EventId,
                Evidence(proposalArea, decisionArea));
            accepted.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), owner.Id, 2, DateTimeOffset.UtcNow));
            _events.Add(owner.Stream,
            [
                new(owner.Stream, 0, proposalArea, proposal),
                new(owner.Stream, 1, decisionArea, accepted),
            ]);
        }

        public void Remove(string identifier, ulong proposalArea, ulong decisionArea)
        {
            var owner = new CriteriaEntry(EditionId, identifier);
            var events = _events[owner.Stream];
            var operation = Uuid.CreateVersion4();
            var previous = (CriterionChangeProposed)events[0].Event;
            var intent = new CriterionChangeIntent(EditionId, _generation, identifier, operation, 1, "remove",
                null, null, null, null, null);
            var (admission, claim) = Claim(intent, 2, proposalArea - 10);
            var proposal = new CriterionChangeProposed(intent, 2, previous.IncarnationId,
                Actor, admission, null, claim.Metadata.EventId);
            proposal.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), owner.Id, 3, DateTimeOffset.UtcNow));
            var accepted = new CriterionChanged(EditionId, identifier, operation, 2, proposal.Metadata.EventId,
                Evidence(proposalArea, decisionArea));
            accepted.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), owner.Id, 4, DateTimeOffset.UtcNow));
            events.Add(new CriteriaOrderedSourceEvent(owner.Stream, 2, proposalArea, proposal));
            events.Add(new CriteriaOrderedSourceEvent(owner.Stream, 3, decisionArea, accepted));
        }

        (AccessAdmissionEvidence Admission, CriterionEntryOperationClaimed Claim) Claim(
            CriterionChangeIntent intent, long generation, ulong area)
        {
            var identity = new AccessAdmissionSourceVersion("stream://bdgrz/identities/actor", "generation", 1);
            var policy = new AccessAdmissionSourceVersion("stream://bdgrz/policies/current", "generation", 1);
            var admission = new AccessAdmissionEvidence(Uuid.CreateVersion4(), Actor.Subject, Actor.Issuer,
                intent.OperationId, CriteriaEntry.StreamFor(EditionId, intent.Identifier).ToString(),
                CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(intent), generation),
                DateTimeOffset.UtcNow, "policy-v1", identity, policy)
            {
                FormatVersion = 1,
                PolicyContentSha256 = new string('a', 64),
                Operator = new AccessAdmissionSourceVersion("stream://bdgrz/operators/actor", "generation", 1)
            };
            var owner = new CriteriaEntryOperation(EditionId, intent.Identifier, intent.OperationId);
            var claim = new CriterionEntryOperationClaimed(intent, null, generation, Actor, admission);
            claim.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), owner.Id, 1, DateTimeOffset.UtcNow));
            _claims.Add(claim.Metadata.EventId, new CriteriaOrderedSourceEvent(owner.Stream, 0, area, claim));
            return (admission, claim);
        }

        CriteriaEntryDecisionEvidence Evidence(ulong proposalArea, ulong decisionArea) => decisionArea > _publication.AreaOffset
            ? new CriteriaEntryDecisionEvidence("publication", PublicationId, _publication.ResourceOffset, _publication.AreaOffset, proposalArea)
            : new CriteriaEntryDecisionEvidence("draft", _draft.Event.Metadata.EventId, _draft.ResourceOffset, _draft.AreaOffset, proposalArea);

        public void WithholdEditionCoverage() => _editionCovered = false;

        public void RemoveClaim(string identifier)
        {
            var proposal = (CriterionChangeProposed)_events[new CriteriaEntry(EditionId, identifier).Stream][0].Event;
            _claims.Remove(proposal.OperationClaimEventId);
        }

        public ValueTask<Result<CriteriaPublicationProof>> ReadPublishedAsync(Uuid editionId, CancellationToken ct = default) =>
            ValueTask.FromResult(Result<CriteriaPublicationProof>.Success(new CriteriaPublicationProof(editionId, PublicationId, 4)));

        public ValueTask<bool> CoversAsync(EventStreamAddress stream, ulong nextResourceOffset, ulong cutAreaOffset,
            CancellationToken ct = default) => ValueTask.FromResult(_covered && (_editionCovered || stream != _publication.Stream));

        public ValueTask<CriteriaOrderedSourceEvent?> FindAsync(EventStreamAddress stream, Uuid aggregateId,
            Uuid eventId, CancellationToken ct = default) => ValueTask.FromResult<CriteriaOrderedSourceEvent?>(
                stream == _publication.Stream && eventId == PublicationId ? _publication :
                stream == _draft.Stream && eventId == _draft.Event.Metadata.EventId ? _draft :
                _claims.TryGetValue(eventId, out var claim) && claim.Stream == stream ? claim :
                _events.TryGetValue(stream, out var records) ? records.FirstOrDefault(x => x.Event.Metadata.AggregateId == aggregateId && x.Event.Metadata.EventId == eventId) : null);

        public async IAsyncEnumerable<CriteriaOrderedSourceEvent> ReadAsync(EventStreamAddress stream, Uuid aggregateId,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            if (_events.TryGetValue(stream, out var events))
                foreach (var item in events)
                    yield return item;
        }
    }
}
