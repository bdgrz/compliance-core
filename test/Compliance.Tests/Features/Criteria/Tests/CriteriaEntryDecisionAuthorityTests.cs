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

public sealed class CriteriaEntryDecisionAuthorityTests
{
    [Fact]
    public async Task ShouldRemainPendingGivenUnqualifiedDraftHead()
    {
        // Arrange
        var fixture = new Fixture { CurrentHead = false };

        // Act
        var result = await fixture.DecideAsync();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ShouldPermitWitnessCandidateGivenQualifiedCurrentDraft()
    {
        // Arrange
        var fixture = new Fixture();

        // Act
        var result = await fixture.DecideAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Accepted);
        Assert.Equal("draft", result.Evidence.Basis);
        Assert.Equal(fixture.Draft.Event.Metadata.EventId, result.Evidence.EditionEventId);
        Assert.Equal(fixture.Proposal.AreaOffset, result.Evidence.ProposalAreaOffset);
    }

    [Fact]
    public async Task ShouldPermitIndependentEntryGivenPublicationAttemptInProgress()
    {
        // Arrange
        var fixture = new Fixture();
        var started = fixture.StartPublication(7);

        // Act
        var result = await fixture.DecideAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Accepted);
        Assert.Equal("draft", result.Evidence.Basis);
        Assert.Equal(started.Event.Metadata.EventId, result.Evidence.EditionEventId);
    }

    [Fact]
    public async Task ShouldRemainPendingGivenDraftCancellationAfterAnInvalidHistory()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.CancelDraft(7);

        // Act
        var result = await fixture.DecideAsync();

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData(6UL, true)]
    [InlineData(10UL, false)]
    public async Task ShouldClassifyProposalGivenQualifiedPublicationCut(ulong proposalArea, bool accepted)
    {
        // Arrange
        var fixture = new Fixture(proposalArea);
        fixture.Publish(8);

        // Act
        var result = await fixture.DecideAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(accepted, result.Accepted);
        Assert.Equal(accepted ? null : "edition_published", result.Reason);
        Assert.Equal("publication", result.Evidence.Basis);
    }

    [Fact]
    public async Task ShouldRemainPendingGivenMissingExactClaim()
    {
        // Arrange
        var fixture = new Fixture { ShowClaim = false };

        // Act
        var result = await fixture.DecideAsync();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ShouldRemainPendingGivenUnqualifiedSourceCoverage()
    {
        // Arrange
        var fixture = new Fixture { Covered = false };

        // Act
        var result = await fixture.DecideAsync();

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRemainPendingGivenMissingOrRejectedParticipantCausality(bool rejected)
    {
        // Arrange
        var fixture = new Fixture();

        // Act
        var result = await fixture.DecideWithoutCausalityAsync(rejected);

        // Assert
        Assert.Null(result);
    }

    sealed class RejectedCausality : ICriteriaParticipantCausalOrder
    {
        public ValueTask<bool> PrecedesAsync(CriteriaOrderedSourceEvent first, CriteriaOrderedSourceEvent second,
            CancellationToken ct = default) => ValueTask.FromResult(false);
    }

    sealed class Fixture : ICriteriaExactSourceReader, ICriteriaSourceCutCoverage,
        ICriteriaPublicationAuthority, ICriteriaFreshEntryHead
    {
        readonly Dictionary<EventStreamAddress, List<CriteriaOrderedSourceEvent>> _facts = [];
        readonly Uuid _edition = Uuid.CreateVersion4();
        readonly Uuid _draft = Uuid.CreateVersion4();
        readonly Uuid _entryOperation = Uuid.CreateVersion4();
        readonly Uuid _user = Uuid.CreateVersion4();
        CriteriaPublicationProof? _published;
        readonly EventStreamAddress? _claimStream;

        public Fixture(ulong proposalArea = 6)
        {
            var actor = new ActorAttribution(_user.ToString(), "bdgrz");
            var edition = new CriteriaEdition(_edition);
            var draftIntent = new DraftCriteriaEdition(_edition, _draft, "tsc", "edition_1");
            var draftAdmission = Admission(actor, _draft, edition.Stream,
                CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(draftIntent), 1));
            var request = Add(edition.Stream, edition.Id, new CriteriaEditionDraftRequested(_edition, _draft, _draft,
                1, "tsc", "edition_1", actor, draftAdmission, null, null), 1);
            var label = new EditionLabelClaim("edition_1");
            var reserved = Add(label.Stream, label.Id, new EditionLabelReserved("edition_1", _edition, _draft, 1,
                request.Event.Metadata.EventId, actor), 1);
            var active = Add(edition.Stream, edition.Id, new CriteriaEditionDraftActivated(_edition, _draft, _draft,
                "tsc", "edition_1", 1, reserved.Event.Metadata.EventId), 3);
            var confirmed = Add(label.Stream, label.Id, new EditionLabelConfirmed("edition_1", _edition, _draft, 1,
                active.Event.Metadata.EventId), 2);
            Draft = Add(edition.Stream, edition.Id, new CriteriaEditionDrafted(_edition, _draft, _draft, "tsc",
                "edition_1", 1, confirmed.Event.Metadata.EventId), 4);

            var target = new CriteriaEntry(_edition, "CC6.1");
            var intent = new CriterionChangeIntent(_edition, _draft, "CC6.1", _entryOperation, 0, "create",
                "criterion", "security", null, null, "summary");
            var entryAdmission = Admission(actor, _entryOperation, target.Stream,
                CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(intent), 1));
            var operation = new CriteriaEntryOperation(_edition, "CC6.1", _entryOperation);
            _claimStream = operation.Stream;
            var claim = Add(operation.Stream, operation.Id,
                new CriterionEntryOperationClaimed(intent, null, 1, actor, entryAdmission), 5);
            Proposal = Add(target.Stream, target.Id, new CriterionChangeProposed(intent, 1,
                CriteriaEntry.IncarnationOf(_edition, "CC6.1", _entryOperation), actor, entryAdmission, null,
                claim.Event.Metadata.EventId), proposalArea);
        }

        public bool Covered { get; init; } = true;
        public bool CurrentHead { get; init; } = true;
        public bool ShowClaim { get; init; } = true;
        public CriteriaOrderedSourceEvent Draft { get; }
        public CriteriaOrderedSourceEvent Proposal { get; }

        public ValueTask<CriteriaEntryDecision?> DecideAsync() =>
            new CriteriaEntryDecisionAuthority(this, this, this, this, CriteriaTestCausalOrder.Qualified).DecideAsync(Proposal, CancellationToken.None);

        public ValueTask<CriteriaEntryDecision?> DecideWithoutCausalityAsync(bool rejected) =>
            new CriteriaEntryDecisionAuthority(this, this, this, this, rejected ? new RejectedCausality() : null)
                .DecideAsync(Proposal, CancellationToken.None);

        public void Publish(ulong area)
        {
            var edition = new CriteriaEdition(_edition);
            var attempt = Uuid.CreateVersion4();
            var witness = Uuid.CreateVersion4();
            var actor = new ActorAttribution(_user.ToString(), "bdgrz");
            var confirmation = ((CriteriaEditionDrafted)Draft.Event).ConfirmationEventId;
            var digest = CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(
                new BeginCriteriaPublication(_edition, attempt, "CC6.1", witness)), 1);
            Add(edition.Stream, edition.Id, new CriteriaPublicationStarted(_edition, _draft, attempt, 1, 1,
                "CC6.1", witness, "edition_1", 1, confirmation, actor, Admission(actor, attempt, edition.Stream, digest)), 7);
            var fact = new CriteriaEditionPublished(_edition, _draft, attempt, 1, "CC6.1",
                witness, "edition_1", 1, Uuid.CreateVersion4(), confirmation, actor);
            var record = Add(edition.Stream, edition.Id, fact, area);
            _published = new CriteriaPublicationProof(_edition, fact.Metadata.EventId, record.ResourceOffset);
        }

        public CriteriaOrderedSourceEvent StartPublication(ulong area)
        {
            var edition = new CriteriaEdition(_edition);
            var attempt = Uuid.CreateVersion4();
            var witness = Uuid.CreateVersion4();
            var actor = new ActorAttribution(_user.ToString(), "bdgrz");
            var digest = CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(
                new BeginCriteriaPublication(_edition, attempt, "CC1.1", witness)), 1);
            var admission = Admission(actor, attempt, edition.Stream, digest);
            var drafted = (CriteriaEditionDrafted)Draft.Event;
            return Add(edition.Stream, edition.Id, new CriteriaPublicationStarted(_edition, _draft, attempt, 1, 1,
                "CC1.1", witness, "edition_1", 1, drafted.ConfirmationEventId, actor, admission), area);
        }

        public void CancelDraft(ulong area)
        {
            var edition = new CriteriaEdition(_edition);
            var operation = Uuid.CreateVersion4();
            var actor = new ActorAttribution(_user.ToString(), "bdgrz");
            var digest = CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(
                new CancelCriteriaEditionDraftRequest(_edition, operation, _draft)), 1);
            Add(edition.Stream, edition.Id, new CriteriaEditionDraftRequestCancelled(_edition, operation, 1, _draft,
                actor, Admission(actor, operation, edition.Stream, digest)), area);
        }

        AccessAdmissionEvidence Admission(ActorAttribution actor, Uuid operation, EventStreamAddress target, string digest)
        {
            var source = new AccessAdmissionSourceVersion("stream://bdgrz/authority/source", "v1", 1);
            return new AccessAdmissionEvidence(_user, actor.Subject, actor.Issuer, operation, target.ToString(), digest,
                DateTimeOffset.UtcNow, "policy", source, source)
            {
                FormatVersion = 1,
                PolicyContentSha256 = new string('a', 64),
                Operator = source
            };
        }

        CriteriaOrderedSourceEvent Add(EventStreamAddress stream, Uuid aggregateId, DomainEvent fact, ulong area)
        {
            if (!_facts.TryGetValue(stream, out var records))
                _facts.Add(stream, records = []);
            var offset = (ulong)records.Count;
            fact.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), aggregateId, offset + 1, DateTimeOffset.UtcNow));
            var record = new CriteriaOrderedSourceEvent(stream, offset, area, fact);
            records.Add(record);
            return record;
        }

        public ValueTask<CriteriaOrderedSourceEvent?> FindAsync(EventStreamAddress stream, Uuid aggregateId,
            Uuid eventId, CancellationToken ct = default) => ValueTask.FromResult(
                !ShowClaim && stream == _claimStream ? null : _facts.TryGetValue(stream, out var records)
                    ? records.FirstOrDefault(record => record.Event.Metadata.EventId == eventId) : null);
        public async IAsyncEnumerable<CriteriaOrderedSourceEvent> ReadAsync(EventStreamAddress stream, Uuid aggregateId,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            if (_facts.TryGetValue(stream, out var records))
                foreach (var record in records)
                { ct.ThrowIfCancellationRequested(); yield return record; }
            await Task.CompletedTask;
        }
        public ValueTask<bool> CoversAsync(EventStreamAddress stream, ulong nextResourceOffset, ulong cutAreaOffset,
            CancellationToken ct = default) => ValueTask.FromResult(Covered);
        public ValueTask<bool> IsCurrentAsync(EventStreamAddress stream, Uuid aggregateId, ulong nextResourceOffset,
            CancellationToken ct = default) => ValueTask.FromResult(CurrentHead);
        public ValueTask<Result<CriteriaPublicationProof>> ReadPublishedAsync(Uuid editionId, CancellationToken ct = default) =>
            ValueTask.FromResult(_published is null
                ? Result<CriteriaPublicationProof>.Failure(new RequestError(RequestErrorKind.Conflict, "pending", true))
                : Result<CriteriaPublicationProof>.Success(_published));
    }
}
