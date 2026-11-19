using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Services;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaEntryDecisionEvidenceVerifierTests
{
    [Fact]
    public async Task ShouldVerifyDraftAcceptanceGivenQualifiedEditionCoverageThroughDecision()
    {
        // Arrange
        var fixture = new Fixture();
        var decision = fixture.Decision("draft", fixture.Draft, accepted: true);

        // Act
        var result = await fixture.Verifier.VerifyAsync(fixture.Proposal, decision, CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaEntryDecisionVerification.Verified, result);
        Assert.Equal(decision.AreaOffset, fixture.CoveredThrough);
    }

    [Fact]
    public async Task ShouldRemainPendingGivenUnqualifiedEditionCoverage()
    {
        // Arrange
        var fixture = new Fixture { Coverage = false };
        var decision = fixture.Decision("draft", fixture.Draft, accepted: true);

        // Act
        var result = await fixture.Verifier.VerifyAsync(fixture.Proposal, decision, CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaEntryDecisionVerification.Pending, result);
    }

    [Fact]
    public async Task ShouldRemainPendingGivenNoCrossAreaCausalProof()
    {
        // Arrange
        var fixture = new Fixture();
        var decision = fixture.Decision("draft", fixture.Draft, accepted: true);

        // Act
        var result = await fixture.VerifyWithoutCausalAsync(decision);

        // Assert
        Assert.Equal(CriteriaEntryDecisionVerification.Pending, result);
    }

    [Fact]
    public async Task ShouldRemainPendingGivenRenameReservationIsUnobserved()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.RenameWithMissingReservation();
        var decision = fixture.Decision("draft", fixture.Draft, accepted: true);

        // Act
        var result = await fixture.Verifier.VerifyAsync(fixture.Proposal, decision, CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaEntryDecisionVerification.Pending, result);
    }

    [Fact]
    public async Task ShouldVerifyPreCutProposalGivenPublicationRacedDecision()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.Publish(30);
        var decision = fixture.Decision("draft", fixture.Draft, accepted: true);

        // Act
        var result = await fixture.Verifier.VerifyAsync(fixture.Proposal, decision, CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaEntryDecisionVerification.Verified, result);
    }

    [Fact]
    public async Task ShouldRejectStaleHeadGivenProposalAfterPublication()
    {
        // Arrange
        var fixture = new Fixture(proposalArea: 35);
        fixture.Publish(30);
        var decision = fixture.Decision("draft", fixture.Draft, accepted: true);

        // Act
        var result = await fixture.Verifier.VerifyAsync(fixture.Proposal, decision, CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaEntryDecisionVerification.Pending, result);
    }

    [Fact]
    public async Task ShouldReportAbandonedGivenAbandonmentRacedDraftAcceptance()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.Abandon(30);
        var decision = fixture.Decision("draft", fixture.Draft, accepted: true);

        // Act
        var result = await fixture.Verifier.VerifyAsync(fixture.Proposal, decision, CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaEntryDecisionVerification.Abandoned, result);
    }

    [Fact]
    public async Task ShouldRemainPendingGivenForgedHeadPosition()
    {
        // Arrange
        var fixture = new Fixture();
        var decision = fixture.Decision("draft", fixture.Draft, accepted: true, overrideHeadArea: 9);

        // Act
        var result = await fixture.Verifier.VerifyAsync(fixture.Proposal, decision, CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaEntryDecisionVerification.Pending, result);
    }

    [Fact]
    public async Task ShouldVerifyPostCutRefusalGivenExactPublication()
    {
        // Arrange
        var fixture = new Fixture(proposalArea: 35);
        var publication = fixture.Publish(30);
        var decision = fixture.Decision("publication", publication, accepted: false);

        // Act
        var result = await fixture.Verifier.VerifyAsync(fixture.Proposal, decision, CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaEntryDecisionVerification.Verified, result);
    }

    [Fact]
    public async Task ShouldAcceptWitnessGivenQualifiedDecisionAfterPublicationStartAndBeforePin()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.Start(15);
        fixture.Decision("draft", fixture.Draft, accepted: true);

        // Act
        var valid = await fixture.VerifyWitnessAsync(50);

        // Assert
        Assert.True(valid);
    }

    [Fact]
    public async Task ShouldRejectWitnessGivenProposalAndDecisionAfterPin()
    {
        // Arrange
        var fixture = new Fixture(proposalArea: 50);
        fixture.Decision("draft", fixture.Draft, accepted: true, decisionArea: 60);

        // Act
        var valid = await fixture.VerifyWitnessAsync(45);

        // Assert
        Assert.False(valid);
    }

    [Fact]
    public async Task ShouldRejectWitnessGivenDecisionAfterPin()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.Decision("draft", fixture.Draft, accepted: true, decisionArea: 60);

        // Act
        var valid = await fixture.VerifyWitnessAsync(45);

        // Assert
        Assert.False(valid);
    }

    [Fact]
    public async Task ShouldKeepWitnessPendingGivenUnqualifiedCoverage()
    {
        // Arrange
        var fixture = new Fixture { Coverage = false };
        fixture.Decision("draft", fixture.Draft, accepted: true);

        // Act
        var valid = await fixture.VerifyWitnessAsync(50);

        // Assert
        Assert.False(valid);
    }

    sealed class Fixture : ICriteriaExactSourceReader, ICriteriaSourceCutCoverage, ICriteriaPublicationAuthority
    {
        readonly Dictionary<EventStreamAddress, List<CriteriaOrderedSourceEvent>> _facts = [];
        readonly Uuid _edition = Uuid.CreateVersion4();
        readonly Uuid _draft = Uuid.CreateVersion4();
        readonly Uuid _operation = Uuid.CreateVersion4();
        CriteriaPublicationProof? _publication;

        public Fixture(ulong proposalArea = 20)
        {
            var edition = new CriteriaEdition(_edition);
            var draftUser = Uuid.CreateVersion4();
            var draftActor = new ActorAttribution(draftUser.ToString(), "bdgrz");
            var draftSource = new AccessAdmissionSourceVersion("stream://bdgrz/authority/source", "v1", 1);
            var draftDigest = CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(
                new DraftCriteriaEdition(_edition, _draft, "tsc", "edition")), 1);
            var draftAdmission = new AccessAdmissionEvidence(draftUser, draftActor.Subject, draftActor.Issuer, _draft,
                edition.Stream.ToString(), draftDigest, DateTimeOffset.UtcNow, "policy", draftSource, draftSource)
            {
                FormatVersion = 1,
                PolicyContentSha256 = new string('a', 64),
                Operator = draftSource
            };
            var request = Add(edition.Stream, edition.Id, new CriteriaEditionDraftRequested(_edition, _draft, _draft,
                1, "tsc", "edition", draftActor, draftAdmission, null, null), 1);
            var label = new EditionLabelClaim("edition");
            var reserved = Add(label.Stream, label.Id, new EditionLabelReserved("edition", _edition, _draft, 1,
                request.Event.Metadata.EventId, draftActor), 1);
            var activated = Add(edition.Stream, edition.Id, new CriteriaEditionDraftActivated(_edition, _draft, _draft,
                "tsc", "edition", 1, reserved.Event.Metadata.EventId), 5);
            var confirmed = Add(label.Stream, label.Id, new EditionLabelConfirmed("edition", _edition, _draft, 1,
                activated.Event.Metadata.EventId), 2);
            var drafted = new CriteriaEditionDrafted(_edition, _draft, _draft, "tsc", "edition", 1, confirmed.Event.Metadata.EventId);
            Draft = Add(edition.Stream, edition.Id, drafted, 10);
            var entry = new CriteriaEntry(_edition, "CC6.1");
            var intent = new CriterionChangeIntent(_edition, _draft, "CC6.1", _operation, 0, "create",
                "criterion", "security", null, null, "summary");
            var user = Uuid.CreateVersion4();
            var actor = new ActorAttribution(user.ToString(), "bdgrz");
            var source = new AccessAdmissionSourceVersion("stream://bdgrz/authority/source", "v1", 1);
            var admission = new AccessAdmissionEvidence(user, actor.Subject, actor.Issuer, _operation, entry.Stream.ToString(),
                CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(intent), 1), DateTimeOffset.UtcNow,
                "policy", source, source)
            {
                FormatVersion = 1,
                PolicyContentSha256 = new string('a', 64),
                Operator = source
            };
            var operation = new CriteriaEntryOperation(_edition, "CC6.1", _operation);
            var claim = Add(operation.Stream, operation.Id, new CriterionEntryOperationClaimed(intent, null, 1, actor, admission), 12);
            Proposal = Add(entry.Stream, entry.Id, new CriterionChangeProposed(intent, 1,
                CriteriaEntry.IncarnationOf(_edition, "CC6.1", _operation), actor, admission, null, claim.Event.Metadata.EventId), proposalArea);
            Verifier = new CriteriaEntryDecisionEvidenceVerifier(this, this, this, CriteriaTestCausalOrder.Qualified);
        }

        public bool Coverage { get; init; } = true;
        public ulong CoveredThrough { get; private set; }
        public CriteriaOrderedSourceEvent Draft { get; }
        public CriteriaOrderedSourceEvent Proposal { get; }
        public CriteriaEntryDecisionEvidenceVerifier Verifier { get; }

        public ValueTask<CriteriaEntryDecisionVerification> VerifyWithoutCausalAsync(CriteriaOrderedSourceEvent decision) =>
            new CriteriaEntryDecisionEvidenceVerifier(this, this, this).VerifyAsync(Proposal, decision, CancellationToken.None);

        public void RenameWithMissingReservation()
        {
            var owner = new CriteriaEdition(_edition);
            var operation = Uuid.CreateVersion4();
            var user = Uuid.CreateVersion4();
            var actor = new ActorAttribution(user.ToString(), "bdgrz");
            var source = new AccessAdmissionSourceVersion("stream://bdgrz/authority/source", "v1", 1);
            var digest = CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(
                new BeginCriteriaEditionRename(_edition, operation, "renamed")), 1);
            var admission = new AccessAdmissionEvidence(user, actor.Subject, actor.Issuer, operation,
                owner.Stream.ToString(), digest, DateTimeOffset.UtcNow, "policy", source, source)
            {
                FormatVersion = 1,
                PolicyContentSha256 = new string('a', 64),
                Operator = source
            };
            Add(owner.Stream, owner.Id, new CriteriaEditionRenameRequested(_edition, operation, 1,
                "edition", 1, "renamed", actor, admission), 11);
            Add(owner.Stream, owner.Id, new CriteriaEditionDraftRenamed(_edition, operation,
                "edition", 1, "renamed", 1, Uuid.CreateVersion4()), 15);
        }

        public ValueTask<bool> VerifyWitnessAsync(ulong pinArea) =>
            new CriteriaPublicationWitnessVerifier(this, this, CriteriaTestCausalOrder.Qualified).VerifyAsync(_edition, "CC6.1", _draft,
                ((CriterionChangeProposed)Proposal.Event).IncarnationId, pinArea, null, CancellationToken.None);

        public void Start(ulong area)
        {
            var owner = new CriteriaEdition(_edition);
            var attempt = Uuid.CreateVersion4();
            var witness = ((CriterionChangeProposed)Proposal.Event).IncarnationId;
            var actor = new ActorAttribution("operator", "bdgrz");
            var confirmation = ((CriteriaEditionDrafted)Draft.Event).ConfirmationEventId;
            var source = new AccessAdmissionSourceVersion("stream://bdgrz/authority/source", "v1", 1);
            var digest = CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(
                new BeginCriteriaPublication(_edition, attempt, "CC6.1", witness)), 1);
            var admission = new AccessAdmissionEvidence(Uuid.CreateVersion4(), actor.Subject, actor.Issuer, attempt,
                owner.Stream.ToString(), digest, DateTimeOffset.UtcNow, "policy", source, source)
            {
                FormatVersion = 1,
                PolicyContentSha256 = new string('a', 64),
                Operator = source
            };
            Add(owner.Stream, owner.Id, new CriteriaPublicationStarted(_edition, _draft, attempt, 1, 1, "CC6.1",
                witness, "edition", 1, confirmation, actor, admission), area);
        }

        public CriteriaOrderedSourceEvent Publish(ulong area)
        {
            var owner = new CriteriaEdition(_edition);
            var attempt = Uuid.CreateVersion4();
            var witness = Uuid.CreateVersion4();
            var actor = new ActorAttribution("operator", "bdgrz");
            var confirmation = ((CriteriaEditionDrafted)Draft.Event).ConfirmationEventId;
            var source = new AccessAdmissionSourceVersion("stream://bdgrz/authority/source", "v1", 1);
            var digest = CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(
                new BeginCriteriaPublication(_edition, attempt, "CC6.1", witness)), 1);
            var admission = new AccessAdmissionEvidence(Uuid.CreateVersion4(), actor.Subject, actor.Issuer, attempt,
                owner.Stream.ToString(), digest, DateTimeOffset.UtcNow, "policy", source, source)
            {
                FormatVersion = 1,
                PolicyContentSha256 = new string('a', 64),
                Operator = source
            };
            Add(owner.Stream, owner.Id, new CriteriaPublicationStarted(_edition, _draft, attempt, 1, 1, "CC6.1",
                witness, "edition", 1, confirmation, actor, admission), 25);
            var published = new CriteriaEditionPublished(_edition, _draft, attempt, 1, "CC6.1",
                witness, "edition", 1, Uuid.CreateVersion4(), confirmation, actor);
            var record = Add(owner.Stream, owner.Id, published, area);
            _publication = new CriteriaPublicationProof(_edition, published.Metadata.EventId, record.ResourceOffset);
            return record;
        }

        public void Abandon(ulong area)
        {
            var owner = new CriteriaEdition(_edition);
            var operation = Uuid.CreateVersion4();
            var actor = new ActorAttribution("operator", "bdgrz");
            var source = new AccessAdmissionSourceVersion("stream://bdgrz/authority/source", "v1", 1);
            var digest = CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(
                new AbandonCriteriaEditionDraft(_edition, operation)), 1);
            var admission = new AccessAdmissionEvidence(Uuid.CreateVersion4(), actor.Subject, actor.Issuer, operation,
                owner.Stream.ToString(), digest, DateTimeOffset.UtcNow, "policy", source, source)
            {
                FormatVersion = 1,
                PolicyContentSha256 = new string('a', 64),
                Operator = source
            };
            Add(owner.Stream, owner.Id, new CriteriaEditionDraftAbandoned(_edition, _draft, operation, 1,
                "edition", 1, actor, admission), area);
        }

        public CriteriaOrderedSourceEvent Decision(string basis, CriteriaOrderedSourceEvent head, bool accepted,
            ulong? overrideHeadArea = null, ulong decisionArea = 40)
        {
            var owner = new CriteriaEntry(_edition, "CC6.1");
            var evidence = new CriteriaEntryDecisionEvidence(basis, head.Event.Metadata.EventId, head.ResourceOffset,
                overrideHeadArea ?? head.AreaOffset, Proposal.AreaOffset);
            DomainEvent fact = accepted
                ? new CriterionChanged(_edition, "CC6.1", _operation, 1, Proposal.Event.Metadata.EventId, evidence)
                : new CriterionChangeRefused(_edition, "CC6.1", _operation, 1, Proposal.Event.Metadata.EventId,
                    "edition_published", evidence);
            return Add(owner.Stream, owner.Id, fact, decisionArea);
        }

        CriteriaOrderedSourceEvent Add(EventStreamAddress stream, Uuid owner, DomainEvent fact, ulong area)
        {
            if (!_facts.TryGetValue(stream, out var list))
                _facts.Add(stream, list = []);
            var position = (ulong)list.Count;
            fact.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), owner, position + 1, DateTimeOffset.UtcNow));
            var record = new CriteriaOrderedSourceEvent(stream, position, area, fact);
            list.Add(record);
            return record;
        }

        public ValueTask<CriteriaOrderedSourceEvent?> FindAsync(EventStreamAddress stream, Uuid aggregateId,
            Uuid eventId, CancellationToken ct = default) => ValueTask.FromResult(
                _facts.TryGetValue(stream, out var list) ? list.FirstOrDefault(x => x.Event.Metadata.EventId == eventId) : null);

        public async IAsyncEnumerable<CriteriaOrderedSourceEvent> ReadAsync(EventStreamAddress stream, Uuid aggregateId,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            if (_facts.TryGetValue(stream, out var list))
                foreach (var value in list)
                { ct.ThrowIfCancellationRequested(); yield return value; }
            await Task.CompletedTask;
        }

        public ValueTask<bool> CoversAsync(EventStreamAddress stream, ulong nextResourceOffset, ulong cutAreaOffset,
            CancellationToken ct = default)
        {
            if (stream == CriteriaEdition.AddressOf(_edition))
                CoveredThrough = cutAreaOffset;
            return ValueTask.FromResult(Coverage);
        }

        public ValueTask<Result<CriteriaPublicationProof>> ReadPublishedAsync(Uuid editionId, CancellationToken ct = default) =>
            ValueTask.FromResult(_publication is null
                ? Result<CriteriaPublicationProof>.Failure(new RequestError(RequestErrorKind.Conflict, "pending", true))
                : Result<CriteriaPublicationProof>.Success(_publication));
    }
}
