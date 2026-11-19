using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Readers;
using Bdgrz.Compliance.Features.Criteria.Services;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaEditionValueReceiptReaderTests
{
    [Fact]
    public async Task ShouldReturnAcceptedGivenExactAdmittedDraftClaimAndQualifiedDecision()
    {
        // Arrange
        var fixture = new Fixture();

        // Act
        var result = await fixture.ReadAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("accepted", result.Value!.Status);
        Assert.Null(result.Value.Reason);
    }

    [Fact]
    public async Task ShouldReturnRefusedGivenVerifiedPostPublicationProposal()
    {
        // Arrange
        var fixture = new Fixture(refused: true, disclosure: true);

        // Act
        var result = await fixture.ReadAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("refused", result.Value!.Status);
        Assert.Equal("edition_published", result.Value.Reason);
    }

    [Theory]
    [InlineData("claim")]
    [InlineData("confirmation")]
    [InlineData("outcome")]
    public async Task ShouldRemainPendingGivenRequiredSourceFactIsUnobserved(string missing)
    {
        // Arrange
        var fixture = new Fixture { Missing = missing };

        // Act
        var result = await fixture.ReadAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("pending", result.Value!.Status);
    }

    [Fact]
    public async Task ShouldRemainPendingGivenCoverageCannotQualifyTheSource()
    {
        // Arrange
        var fixture = new Fixture { Covered = false };

        // Act
        var result = await fixture.ReadAsync();

        // Assert
        Assert.Equal("pending", result.Value!.Status);
    }

    [Fact]
    public async Task ShouldRemainPendingGivenMissingCrossAreaCausalityProvider()
    {
        // Arrange
        var fixture = new Fixture();
        var reader = new SourceCriteriaEditionValueOperationReader(fixture, fixture, fixture, fixture);

        // Act
        var result = await reader.ReadAsync(fixture.EditionId, "field", "coverage_note", null, null, fixture.OperationId);

        // Assert
        Assert.Equal("pending", result.Value!.Status);
    }

    [Fact]
    public async Task ShouldRemainPendingGivenDecisionEvidenceNamesWrongProposalPosition()
    {
        // Arrange
        var fixture = new Fixture(badDecisionEvidence: true);

        // Act
        var result = await fixture.ReadAsync();

        // Assert
        Assert.Equal("pending", result.Value!.Status);
    }

    [Fact]
    public async Task ShouldNotClaimAcceptanceGivenAbandonmentWonBeforeDraftBasedOutcome()
    {
        // Arrange
        var fixture = new Fixture(abandonedBeforeOutcome: true);

        // Act
        var result = await fixture.ReadAsync();

        // Assert
        Assert.Equal("pending", result.Value!.Status);
    }

    [Fact]
    public async Task ShouldRejectAmbiguousTargetGivenTheTargetShapeIsAmbiguous()
    {
        // Arrange
        var fixture = new Fixture();
        var reader = new SourceCriteriaEditionValueOperationReader(fixture, fixture, fixture, fixture);

        // Act
        var result = await reader.ReadAsync(fixture.EditionId, "field", "coverage_note", "security", null,
            fixture.OperationId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
    }

    sealed class Fixture : ICriteriaExactSourceReader, ICriteriaSourceCutCoverage, ICriteriaPublicationAuthority,
        ICriteriaFreshEntryHead, ICriteriaParticipantCausalOrder
    {
        readonly Dictionary<EventStreamAddress, List<CriteriaOrderedSourceEvent>> _records = [];
        readonly string _kind;
        readonly string? _field;
        readonly string? _category;
        readonly string? _code;
        readonly Uuid _publicationId;
        readonly ulong _publicationOffset;
        readonly EventStreamAddress _claimStream;
        readonly EventStreamAddress _labelStream;
        readonly Uuid _confirmationId;
        readonly EventStreamAddress _targetStream;
        readonly Uuid _outcomeId;

        public Fixture(bool refused = false, bool disclosure = false, bool badDecisionEvidence = false,
            bool abandonedBeforeOutcome = false)
        {
            EditionId = Uuid.CreateVersion4();
            OperationId = Uuid.CreateVersion4();
            var draftId = Uuid.CreateVersion4();
            var userId = Uuid.CreateVersion4();
            var actor = new ActorAttribution(userId.ToString(), "bdgrz");
            _kind = disclosure ? "disclosure" : "field";
            _field = disclosure ? null : "coverage_note";
            _category = disclosure ? "security" : null;
            _code = disclosure ? "support_1" : null;
            var edition = new CriteriaEdition(EditionId);
            var target = new CriteriaEditionValue(EditionId, _kind, _field, _category, _code, OperationId);
            var operation = new CriteriaEditionValueOperation(EditionId, _kind, _field, _category, _code, OperationId);
            var label = new EditionLabelClaim("edition_1");
            var draftCommand = new DraftCriteriaEdition(EditionId, draftId, "tsc", "edition_1");
            var draftAdmission = Admission(userId, actor, draftId, edition.Stream,
                CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(draftCommand), 1));
            var request = Add(edition, new CriteriaEditionDraftRequested(EditionId, draftId, draftId, 1, "tsc", "edition_1",
                actor, draftAdmission, null, null), 1);
            var reservation = Add(label, new EditionLabelReserved("edition_1", EditionId, draftId, 1,
                request.Event.Metadata.EventId, actor), 1);
            var activation = Add(edition, new CriteriaEditionDraftActivated(EditionId, draftId, draftId, "tsc", "edition_1", 1,
                reservation.Event.Metadata.EventId), 2);
            var confirmation = Add(label, new EditionLabelConfirmed("edition_1", EditionId, draftId, 1,
                activation.Event.Metadata.EventId), 2);
            _confirmationId = confirmation.Event.Metadata.EventId;
            var drafted = Add(edition, new CriteriaEditionDrafted(EditionId, draftId, draftId, "tsc", "edition_1", 1,
                _confirmationId), 3);
            var intent = new CriteriaEditionValueIntent(EditionId, draftId, _kind, _field, _category, _code,
                OperationId, 0, "Partial.", null);
            var admission = Admission(userId, actor, OperationId, target.Stream,
                CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(intent), 1));
            var claim = Add(operation, new CriteriaEditionValueOperationClaimed(intent, 1, actor, admission), 4);
            _claimStream = operation.Stream;
            CriteriaOrderedSourceEvent? published = null;
            if (refused)
            {
                var attempt = Uuid.CreateVersion4();
                var incarnation = Uuid.CreateVersion4();
                var begin = new BeginCriteriaPublication(EditionId, attempt, "CC6.1", incarnation);
                Add(edition, new CriteriaPublicationStarted(EditionId, draftId, attempt, 1, 1, "CC6.1", incarnation,
                    "edition_1", 1, _confirmationId, actor, Admission(userId, actor, attempt, edition.Stream,
                        CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(begin), 1))), 5);
                published = Add(edition, new CriteriaEditionPublished(EditionId, draftId, attempt, 1, "CC6.1",
                    incarnation, "edition_1", 1, Uuid.CreateVersion4(), _confirmationId, actor), 6);
            }
            if (abandonedBeforeOutcome)
            {
                var abandonId = Uuid.CreateVersion4();
                var command = new AbandonCriteriaEditionDraft(EditionId, abandonId);
                var abandonAdmission = Admission(userId, actor, abandonId, edition.Stream,
                    CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(command), 1));
                Add(edition, new CriteriaEditionDraftAbandoned(EditionId, draftId, abandonId, 1, "edition_1", 1,
                    actor, abandonAdmission), 6);
            }
            _publicationId = published?.Event.Metadata.EventId ?? Uuid.Empty;
            _publicationOffset = published?.ResourceOffset ?? 0;
            var proposalArea = refused ? 7UL : 5UL;
            var proposal = Add(target, new CriteriaEditionValueProposed(intent, 1, request.Event.Metadata.EventId,
                claim.Event.Metadata.EventId, actor, admission), proposalArea);
            var evidence = refused
                ? new CriteriaEditionValueDecisionEvidence("publication", _publicationId, _publicationOffset, 6, proposalArea)
                : new CriteriaEditionValueDecisionEvidence("draft", drafted.Event.Metadata.EventId, drafted.ResourceOffset, 3,
                    badDecisionEvidence ? proposalArea + 1 : proposalArea);
            DomainEvent outcome = refused
                ? new CriteriaEditionValueRefused(EditionId, OperationId, 1, proposal.Event.Metadata.EventId, "edition_published", evidence)
                : new CriteriaEditionValueAccepted(EditionId, OperationId, 1, proposal.Event.Metadata.EventId, evidence);
            var outcomeRecord = Add(target, outcome, refused ? 8UL : abandonedBeforeOutcome ? 7UL : 6UL);
            _outcomeId = outcomeRecord.Event.Metadata.EventId;
            _targetStream = target.Stream;
            _labelStream = label.Stream;
        }

        public Uuid EditionId { get; }
        public Uuid OperationId { get; }
        public bool Covered { get; init; } = true;
        public string? Missing { get; init; }
        public ValueTask<Result<CriteriaOperationReceipt>> ReadAsync() =>
            new SourceCriteriaEditionValueOperationReader(this, this, this, this, this).ReadAsync(EditionId, _kind, _field, _category, _code, OperationId);
        public ValueTask<bool> CoversAsync(EventStreamAddress stream, ulong nextResourceOffset, ulong cutAreaOffset, CancellationToken ct = default) =>
            ValueTask.FromResult(Covered);
        public ValueTask<bool> IsCurrentAsync(EventStreamAddress stream, Uuid aggregateId, ulong nextResourceOffset, CancellationToken ct = default) =>
            ValueTask.FromResult(Covered);
        public ValueTask<bool> PrecedesAsync(CriteriaOrderedSourceEvent first, CriteriaOrderedSourceEvent second,
            CancellationToken ct = default) => ValueTask.FromResult(true);
        public ValueTask<Result<CriteriaPublicationProof>> ReadPublishedAsync(Uuid editionId, CancellationToken ct = default) =>
            ValueTask.FromResult(Result<CriteriaPublicationProof>.Success(new CriteriaPublicationProof(editionId, _publicationId, _publicationOffset)));
        public ValueTask<CriteriaOrderedSourceEvent?> FindAsync(EventStreamAddress stream, Uuid aggregateId, Uuid eventId,
            CancellationToken ct = default) => ValueTask.FromResult(_records.TryGetValue(stream, out var records)
                ? records.FirstOrDefault(item => item.Event.Metadata.AggregateId == aggregateId && item.Event.Metadata.EventId == eventId && Visible(item)) : null);
        public async IAsyncEnumerable<CriteriaOrderedSourceEvent> ReadAsync(EventStreamAddress stream, Uuid aggregateId,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            if (_records.TryGetValue(stream, out var records))
                foreach (var item in records)
                    if (item.Event.Metadata.AggregateId == aggregateId && Visible(item))
                        yield return item;
        }
        bool Visible(CriteriaOrderedSourceEvent item) => !(Missing == "claim" && item.Stream == _claimStream ||
            Missing == "confirmation" && item.Stream == _labelStream && item.Event.Metadata.EventId == _confirmationId ||
            Missing == "outcome" && item.Stream == _targetStream && item.Event.Metadata.EventId == _outcomeId);
        CriteriaOrderedSourceEvent Add(Aggregate owner, DomainEvent fact, ulong area)
        {
            if (!_records.TryGetValue(owner.Stream, out var records))
                _records.Add(owner.Stream, records = []);
            fact.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), owner.Id, (ulong)records.Count + 1,
                new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero)));
            var item = new CriteriaOrderedSourceEvent(owner.Stream, (ulong)records.Count, area, fact);
            records.Add(item);
            return item;
        }
        static AccessAdmissionEvidence Admission(Uuid userId, ActorAttribution actor, Uuid operationId,
            EventStreamAddress target, string digest)
        {
            var version = new AccessAdmissionSourceVersion("stream://bdgrz/authority/source", "v1", 1);
            return new AccessAdmissionEvidence(userId, actor.Subject, actor.Issuer, operationId, target.ToString(), digest,
                new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero), "policy", version, version)
            {
                FormatVersion = 1,
                PolicyContentSha256 = new string('a', 64),
                Operator = version
            };
        }
    }
}
