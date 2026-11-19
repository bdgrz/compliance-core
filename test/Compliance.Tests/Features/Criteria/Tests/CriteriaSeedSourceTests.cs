using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Services;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Models;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaSeedSourceTests
{
    [Fact]
    public async Task ShouldResolveSeedParentGivenExactEarlierBindingAndAcceptance()
    {
        // Arrange
        var fixture = new Fixture(includeBinding: true, bindingArea: 1);

        // Act
        var result = await fixture.ReadParentAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(fixture.Accepted.Metadata.EventId, result.Value!.AcceptanceEventId);
    }

    [Fact]
    public async Task ShouldLeaveParentPendingGivenDecisionCoverageIsUnavailable()
    {
        // Arrange
        var fixture = new Fixture(includeBinding: true, bindingArea: 1);

        // Act
        var result = await fixture.ReadParentWithoutCoverageAsync();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
    }

    [Fact]
    public async Task ShouldLeaveParentPendingGivenTheBindingHasNotBeenObserved()
    {
        // Arrange
        var fixture = new Fixture(includeBinding: false, bindingArea: 1);

        // Act
        var result = await fixture.ReadParentAsync();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
    }

    [Fact]
    public async Task ShouldLeaveParentPendingGivenTheOperationClaimHasNotBeenObserved()
    {
        // Arrange
        var fixture = new Fixture(includeBinding: true, bindingArea: 1, includeClaim: false);

        // Act
        var result = await fixture.ReadParentAsync();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
    }

    [Fact]
    public async Task ShouldRejectSeedParentGivenBindingFollowsItsProposal()
    {
        // Arrange
        var fixture = new Fixture(includeBinding: true, bindingArea: 5);

        // Act
        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await fixture.ReadParentAsync());
    }

    [Fact]
    public async Task ShouldResolveThePublishedSeedEntryGivenItsSourceChainAndCoverage()
    {
        // Arrange
        var fixture = new Fixture(includeBinding: true, bindingArea: 1);

        // Act
        var result = await fixture.ReadEntryAsync();

        // Assert
        Assert.Equal(CriteriaEntryAuthorityStatus.Accepted, result.Value!.Status);
        Assert.Equal(fixture.Accepted.Metadata.EventId, result.Value.Proof!.DecisionEventId);
    }

    [Fact]
    public async Task ShouldKeepThePublishedSeedEntryPendingGivenAnUnobservedOperationClaim()
    {
        // Arrange
        var fixture = new Fixture(includeBinding: true, bindingArea: 1, includeClaim: false);

        // Act
        var result = await fixture.ReadEntryAsync();

        // Assert
        Assert.Equal(CriteriaEntryAuthorityStatus.Pending, result.Value!.Status);
    }

    [Fact]
    public async Task ShouldKeepThePublishedSeedEntryPendingGivenAnUnqualifiedLaterExecution()
    {
        // Arrange
        var fixture = new Fixture(includeBinding: true, bindingArea: 1, proposalGeneration: 2);

        // Act
        var result = await fixture.ReadEntryAsync();

        // Assert
        Assert.Equal(CriteriaEntryAuthorityStatus.Pending, result.Value!.Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldKeepSeedEvidencePendingGivenMissingOrRejectedCausality(bool rejected)
    {
        // Arrange
        var fixture = new Fixture(includeBinding: true, bindingArea: 1);

        // Act
        var parent = await fixture.ReadParentWithoutCausalityAsync(rejected);
        var entry = await fixture.ReadEntryWithoutCausalityAsync(rejected);

        // Assert
        Assert.False(parent.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, parent.Error!.Kind);
        Assert.Equal(CriteriaEntryAuthorityStatus.Pending, entry.Value!.Status);
    }

    sealed class RejectedCausality : ICriteriaParticipantCausalOrder
    {
        public ValueTask<bool> PrecedesAsync(CriteriaOrderedSourceEvent first, CriteriaOrderedSourceEvent second,
            CancellationToken ct = default) => ValueTask.FromResult(false);
    }

    sealed class Fixture : ICriteriaExactSourceReader, ICriteriaPublicationAuthority, ICriteriaSourceCutCoverage
    {
        readonly List<CriteriaOrderedSourceEvent> _events = [];
        readonly Criterion _entry;
        readonly CriterionSeedProposed _proposal;
        readonly CriteriaEditionPublished _publication;

        public Fixture(bool includeBinding, ulong bindingArea, bool includeClaim = true, long proposalGeneration = 1)
        {
            var edition = PlatformCriteriaCatalog.SeedEdition();
            _entry = PlatformCriteriaCatalog.EnumerateSeedEntries().First(x => x.Kind == "criterion");
            var owner = new CriteriaEntry(edition.EditionId, _entry.Identifier);
            var editionOwner = new CriteriaEdition(edition.EditionId);
            var draft = PlatformCriteriaSeedDefinition.OperationOf(edition.EditionId, "edition");
            var operation = PlatformCriteriaSeedDefinition.OperationOf(edition.EditionId, "entry:" + _entry.Identifier);
            var fingerprint = CriteriaSeedFingerprint.Compute(edition, PlatformCriteriaCatalog.EnumerateSeedEntries());
            var actor = new ActorAttribution("criteria-seed-bootstrap", "system");
            var request = DomainEventSeed.Attach(new CriteriaEditionDraftRequested(edition.EditionId, draft, draft,
                1, "tsc", edition.EditionLabel, actor, null,
                CriteriaSeedFingerprint.Version, fingerprint), editionOwner.Id, 1);
            var label = new EditionLabelClaim(edition.EditionLabel);
            var reservation = DomainEventSeed.Attach(new EditionLabelReserved(edition.EditionLabel, edition.EditionId,
                draft, 1, request.Metadata.EventId, actor), label.Id, 1);
            var activation = DomainEventSeed.Attach(new CriteriaEditionDraftActivated(edition.EditionId, draft, draft,
                "tsc", edition.EditionLabel, 1, reservation.Metadata.EventId), editionOwner.Id, 2);
            var confirmation = DomainEventSeed.Attach(new EditionLabelConfirmed(edition.EditionLabel, edition.EditionId,
                draft, 1, activation.Metadata.EventId), label.Id, 2);
            var intent = new CriterionChangeIntent(edition.EditionId, draft, _entry.Identifier, operation, 0,
                "create", _entry.Kind, _entry.Category, null, null, _entry.Summary);
            var incarnation = CriteriaEntry.IncarnationOf(edition.EditionId, _entry.Identifier, operation);
            var operationId = CriteriaEntryOperation.IdentityOf(edition.EditionId, _entry.Identifier, operation);
            var operationAddress = CriteriaEntryOperation.AddressOf(edition.EditionId, _entry.Identifier, operation);
            var claim = DomainEventSeed.Attach(new CriterionEntryOperationSeedClaimed(intent, 1, incarnation, null,
                request.Metadata.EventId, CriteriaSeedFingerprint.Version, fingerprint), operationId, 1);
            _proposal = DomainEventSeed.Attach(new CriterionSeedProposed(intent, proposalGeneration,
                incarnation, null, request.Metadata.EventId, claim.Metadata.EventId,
                CriteriaSeedFingerprint.Version, fingerprint), owner.Id, 1);
            var drafted = DomainEventSeed.Attach(new CriteriaEditionDrafted(edition.EditionId, draft, draft,
                "tsc", edition.EditionLabel, 1, confirmation.Metadata.EventId), editionOwner.Id, 3);
            var evidence = new CriteriaEntryDecisionEvidence("draft", drafted.Metadata.EventId, 2, 3, 5);
            Accepted = DomainEventSeed.Attach(new CriterionChanged(edition.EditionId, _entry.Identifier, operation,
                proposalGeneration, _proposal.Metadata.EventId, evidence), owner.Id, 2);
            var attempt = Uuid.CreateVersion4();
            var started = DomainEventSeed.Attach(new CriteriaPublicationStarted(edition.EditionId, draft, attempt, 1, 1,
                _entry.Identifier, incarnation, edition.EditionLabel, 1, confirmation.Metadata.EventId, actor, null),
                editionOwner.Id, 4);
            _publication = DomainEventSeed.Attach(new CriteriaEditionPublished(edition.EditionId, draft,
                attempt, 1, _entry.Identifier, incarnation, edition.EditionLabel, 1,
                Uuid.CreateVersion4(), confirmation.Metadata.EventId, actor), editionOwner.Id, 5);
            if (includeBinding)
                _events.Add(new CriteriaOrderedSourceEvent(editionOwner.Stream, 0, bindingArea, request));
            _events.Add(new CriteriaOrderedSourceEvent(label.Stream, 0, 1, reservation));
            _events.Add(new CriteriaOrderedSourceEvent(editionOwner.Stream, 1, 2, activation));
            _events.Add(new CriteriaOrderedSourceEvent(label.Stream, 1, 2, confirmation));
            _events.Add(new CriteriaOrderedSourceEvent(editionOwner.Stream, 2, 3, drafted));
            if (includeClaim)
                _events.Add(new CriteriaOrderedSourceEvent(operationAddress, 0, 4, claim));
            _events.Add(new CriteriaOrderedSourceEvent(owner.Stream, 0, 5, _proposal));
            _events.Add(new CriteriaOrderedSourceEvent(owner.Stream, 1, 6, Accepted));
            _events.Add(new CriteriaOrderedSourceEvent(editionOwner.Stream, 3, 9, started));
            _events.Add(new CriteriaOrderedSourceEvent(editionOwner.Stream, 4, 10, _publication));
        }

        public CriterionChanged Accepted { get; }

        public ValueTask<Result<CriteriaParentAcceptance>> ReadParentAsync() =>
            new CriteriaParentAcceptanceReader(this, this, this, CriteriaTestCausalOrder.Qualified).ReadAsync(_entry.EditionId, _entry.Identifier,
                _proposal.IncarnationId, _entry.Category);

        public ValueTask<Result<CriteriaParentAcceptance>> ReadParentWithoutCausalityAsync(bool rejected) =>
            new CriteriaParentAcceptanceReader(this, this, this, rejected ? new RejectedCausality() : null)
                .ReadAsync(_entry.EditionId, _entry.Identifier, _proposal.IncarnationId, _entry.Category);

        public ValueTask<Result<CriteriaParentAcceptance>> ReadParentWithoutCoverageAsync() =>
            new CriteriaParentAcceptanceReader(this).ReadAsync(_entry.EditionId, _entry.Identifier,
                _proposal.IncarnationId, _entry.Category);

        public ValueTask<Result<CriteriaEntryAuthorityResult>> ReadEntryAsync() =>
            new SourceCriteriaEntryAuthority(this, this, this, CriteriaTestCausalOrder.Qualified).ReadAsync(_entry.EditionId, _entry.Identifier);

        public ValueTask<Result<CriteriaEntryAuthorityResult>> ReadEntryWithoutCausalityAsync(bool rejected) =>
            new SourceCriteriaEntryAuthority(this, this, this, rejected ? new RejectedCausality() : null)
                .ReadAsync(_entry.EditionId, _entry.Identifier);

        public ValueTask<Result<CriteriaPublicationProof>> ReadPublishedAsync(Uuid editionId, CancellationToken ct = default) =>
            ValueTask.FromResult(Result<CriteriaPublicationProof>.Success(
                new CriteriaPublicationProof(editionId, _publication.Metadata.EventId, 4)));

        public ValueTask<bool> CoversAsync(EventStreamAddress stream, ulong nextResourceOffset, ulong cutAreaOffset,
            CancellationToken ct = default) => ValueTask.FromResult(true);

        public ValueTask<CriteriaOrderedSourceEvent?> FindAsync(EventStreamAddress stream, Uuid aggregateId,
            Uuid eventId, CancellationToken ct = default) => ValueTask.FromResult(_events.FirstOrDefault(x =>
                x.Stream == stream && x.Event.Metadata.AggregateId == aggregateId && x.Event.Metadata.EventId == eventId));

        public async IAsyncEnumerable<CriteriaOrderedSourceEvent> ReadAsync(EventStreamAddress stream, Uuid aggregateId,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var item in _events.Where(x => x.Stream == stream && x.Event.Metadata.AggregateId == aggregateId))
            {
                ct.ThrowIfCancellationRequested();
                yield return item;
            }
            await Task.CompletedTask;
        }
    }
}
