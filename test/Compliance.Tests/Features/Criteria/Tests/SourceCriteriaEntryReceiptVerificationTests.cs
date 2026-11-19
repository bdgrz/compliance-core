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
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class SourceCriteriaEntryReceiptVerificationTests
{
    [Fact]
    public async Task ShouldReturnAcceptedGivenExactQualifiedEntryDecision()
    {
        // Arrange
        var fixture = new Fixture();

        // Act
        var result = await fixture.ReadAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("accepted", result.Value!.Status);
    }

    [Fact]
    public async Task ShouldRemainPendingGivenAcceptedTargetOutcomeAfterAbandonment()
    {
        // Arrange
        var fixture = new Fixture(abandonedBeforeOutcome: true);

        // Act
        var result = await fixture.ReadAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("pending", result.Value!.Status);
    }

    [Fact]
    public async Task ShouldRemainPendingGivenTerminalReceiptHasNoSourceProviders()
    {
        // Arrange
        var fixture = new Fixture();
        var reader = new SourceCriteriaOperationReader(fixture);

        // Act
        var result = await reader.GetOperationAsync(fixture.EditionId, fixture.OperationId, "CC6.1");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("pending", result.Value!.Status);
    }

    [Fact]
    public async Task ShouldRemainPendingGivenEntryHeadIsUnqualified()
    {
        // Arrange
        var fixture = new Fixture { Qualified = false };

        // Act
        var result = await fixture.ReadAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("pending", result.Value!.Status);
    }

    [Fact]
    public async Task ShouldRemainPendingGivenMissingCausalOrderProvider()
    {
        // Arrange
        var fixture = new Fixture { CausalQualified = false };

        // Act
        var result = await fixture.ReadAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("pending", result.Value!.Status);
    }

    [Fact]
    public async Task ShouldKeepPointOfFocusReceiptPendingGivenMissingParentParticipantOutcome()
    {
        // Arrange
        var fixture = new Fixture(missingParent: true);

        // Act
        var result = await fixture.ReadAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("pending", result.Value!.Status);
    }

    [Fact]
    public async Task ShouldReturnAcceptedGivenExactNoChangeParticipantAndClaim()
    {
        // Arrange
        var fixture = new Fixture(summaryNoChange: true);

        // Act
        var result = await fixture.ReadAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("accepted", result.Value!.Status);
    }

    [Fact]
    public async Task ShouldKeepNoChangeReceiptPendingGivenMissingOperationClaim()
    {
        // Arrange
        var fixture = new Fixture(summaryNoChange: true, omitSummaryClaim: true);

        // Act
        var result = await fixture.ReadAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("pending", result.Value!.Status);
    }

    [Fact]
    public async Task ShouldReturnRefusedGivenExactStaleSummaryParticipantAndClaim()
    {
        // Arrange
        var fixture = new Fixture(summaryRefused: true);

        // Act
        var result = await fixture.ReadAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("refused", result.Value!.Status);
        Assert.Equal("revision_mismatch", result.Value.Reason);
    }

    [Fact]
    public async Task ShouldKeepStaleSummaryRefusalPendingGivenMissingClaim()
    {
        // Arrange
        var fixture = new Fixture(summaryRefused: true, omitSummaryClaim: true);

        // Act
        var result = await fixture.ReadAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("pending", result.Value!.Status);
    }

    sealed class Fixture : IAggregateReader, ICriteriaExactSourceReader, ICriteriaSourceCutCoverage,
        ICriteriaPublicationAuthority, ICriteriaFreshEntryHead
    {
        readonly Dictionary<EventStreamAddress, List<CriteriaOrderedSourceEvent>> _records = [];
        readonly EventStreamAddress _entryStream;
        public Fixture(bool abandonedBeforeOutcome = false, bool missingParent = false,
            bool summaryNoChange = false, bool omitSummaryClaim = false, bool summaryRefused = false)
        {
            EditionId = Uuid.CreateVersion4();
            OperationId = Uuid.CreateVersion4();
            var creationOperation = summaryNoChange || summaryRefused ? Uuid.CreateVersion4() : OperationId;
            var draft = Uuid.CreateVersion4();
            var user = Uuid.CreateVersion4();
            var actor = new ActorAttribution(user.ToString(), "bdgrz");
            var edition = new CriteriaEdition(EditionId);
            var command = new DraftCriteriaEdition(EditionId, draft, "tsc", "edition_1");
            var requestAdmission = Admission(user, actor, draft, edition.Stream,
                CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(command), 1));
            var request = Add(edition, new CriteriaEditionDraftRequested(EditionId, draft, draft, 1, "tsc", "edition_1",
                actor, requestAdmission, null, null), 1);
            var label = new EditionLabelClaim("edition_1");
            var reserved = Add(label, new EditionLabelReserved("edition_1", EditionId, draft, 1, request.Event.Metadata.EventId, actor), 1);
            var activated = Add(edition, new CriteriaEditionDraftActivated(EditionId, draft, draft, "tsc", "edition_1", 1,
                reserved.Event.Metadata.EventId), 2);
            var confirmation = Add(label, new EditionLabelConfirmed("edition_1", EditionId, draft, 1,
                activated.Event.Metadata.EventId), 2);
            var drafted = Add(edition, new CriteriaEditionDrafted(EditionId, draft, draft, "tsc", "edition_1", 1,
                confirmation.Event.Metadata.EventId), 3);
            var target = new CriteriaEntry(EditionId, "CC6.1", OperationId);
            _entryStream = target.Stream;
            var intent = new CriterionChangeIntent(EditionId, draft, "CC6.1", creationOperation, 0, "create",
                missingParent ? "point_of_focus" : "criterion", "security", missingParent ? "CC6" : null,
                missingParent ? Uuid.CreateVersion4() : null, "summary");
            var admission = Admission(user, actor, creationOperation, target.Stream,
                CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(intent), 1));
            var operation = new CriteriaEntryOperation(EditionId, "CC6.1", creationOperation);
            var claim = Add(operation, new CriterionEntryOperationClaimed(intent, null, 1, actor, admission), 4);
            var proposed = Add(target, new CriterionChangeProposed(intent, 1, CriteriaEntry.IncarnationOf(EditionId, "CC6.1", creationOperation),
                actor, admission, missingParent ? Uuid.CreateVersion4() : null, claim.Event.Metadata.EventId), 5);
            if (abandonedBeforeOutcome)
            {
                var abandonId = Uuid.CreateVersion4();
                var abandon = new AbandonCriteriaEditionDraft(EditionId, abandonId);
                var abandonAdmission = Admission(user, actor, abandonId, edition.Stream,
                    CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(abandon), 1));
                Add(edition, new CriteriaEditionDraftAbandoned(EditionId, draft, abandonId, 1, "edition_1", 1,
                    actor, abandonAdmission), 6);
            }
            var evidence = new CriteriaEntryDecisionEvidence("draft", drafted.Event.Metadata.EventId,
                drafted.ResourceOffset, drafted.AreaOffset, proposed.AreaOffset);
            Add(target, new CriterionChanged(EditionId, "CC6.1", creationOperation, 1, proposed.Event.Metadata.EventId, evidence),
                abandonedBeforeOutcome ? 7UL : 6UL);
            if (summaryNoChange || summaryRefused)
            {
                var summary = new CriterionSummaryIntent(EditionId, "CC6.1", OperationId, summaryRefused ? 0 : 1, "summary");
                var summaryAdmission = Admission(user, actor, OperationId, target.Stream,
                    CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(summary), 1));
                var summaryOwner = new CriteriaEntryOperation(EditionId, "CC6.1", OperationId);
                var summaryClaim = Add(summaryOwner, new CriterionEntryOperationClaimed(null, summary, 1, actor, summaryAdmission), 7);
                if (summaryRefused)
                    Add(target, new CriterionSummaryRefused(summary, 1, actor, summaryAdmission, summaryClaim.Event.Metadata.EventId, 1), 8);
                else
                    Add(target, new CriterionSummaryUnchanged(summary, 1, actor, summaryAdmission, summaryClaim.Event.Metadata.EventId), 8);
                if (omitSummaryClaim)
                    _records.Remove(summaryOwner.Stream);
            }
        }
        public Uuid EditionId { get; }
        public Uuid OperationId { get; }
        public bool Qualified { get; init; } = true;
        public bool CausalQualified { get; init; } = true;
        public ValueTask<Result<CriteriaOperationReceipt>> ReadAsync() => new SourceCriteriaOperationReader(this, this, this, this, this,
                causal: CausalQualified ? CriteriaTestCausalOrder.Qualified : null)
            .GetOperationAsync(EditionId, OperationId, "CC6.1");
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
            where TAggregate : Aggregate
        {
            if (aggregate is CriteriaEntry entry && _records.TryGetValue(_entryStream, out var records))
                new AggregateScenario<CriteriaEntry>(entry).Given(records.Select(item => item.Event).ToArray());
            return ValueTask.FromResult(aggregate);
        }
        public ValueTask<CriteriaOrderedSourceEvent?> FindAsync(EventStreamAddress stream, Uuid aggregateId, Uuid eventId,
            CancellationToken ct = default) => ValueTask.FromResult(_records.TryGetValue(stream, out var records)
                ? records.FirstOrDefault(item => item.Event.Metadata.AggregateId == aggregateId && item.Event.Metadata.EventId == eventId) : null);
        public async IAsyncEnumerable<CriteriaOrderedSourceEvent> ReadAsync(EventStreamAddress stream, Uuid aggregateId,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            if (_records.TryGetValue(stream, out var records))
                foreach (var item in records)
                    if (item.Event.Metadata.AggregateId == aggregateId)
                        yield return item;
        }
        public ValueTask<bool> CoversAsync(EventStreamAddress stream, ulong nextResourceOffset, ulong cutAreaOffset,
            CancellationToken ct = default) => ValueTask.FromResult(Qualified);
        public ValueTask<bool> IsCurrentAsync(EventStreamAddress stream, Uuid aggregateId, ulong nextResourceOffset,
            CancellationToken ct = default) => ValueTask.FromResult(Qualified);
        public ValueTask<Result<CriteriaPublicationProof>> ReadPublishedAsync(Uuid editionId, CancellationToken ct = default) =>
            ValueTask.FromResult(Result<CriteriaPublicationProof>.Failure(new RequestError(RequestErrorKind.Conflict, "pending", true)));
        CriteriaOrderedSourceEvent Add(Aggregate owner, DomainEvent fact, ulong area)
        {
            if (!_records.TryGetValue(owner.Stream, out var list))
                _records.Add(owner.Stream, list = []);
            fact.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), owner.Id, (ulong)list.Count + 1,
                new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero)));
            var item = new CriteriaOrderedSourceEvent(owner.Stream, (ulong)list.Count, area, fact);
            list.Add(item);
            return item;
        }
        static AccessAdmissionEvidence Admission(Uuid user, ActorAttribution actor, Uuid operation,
            EventStreamAddress target, string digest)
        {
            var version = new AccessAdmissionSourceVersion("stream://bdgrz/authority/source", "v1", 1);
            return new AccessAdmissionEvidence(user, actor.Subject, actor.Issuer, operation, target.ToString(), digest,
                new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero), "policy", version, version)
            {
                FormatVersion = 1,
                PolicyContentSha256 = new string('a', 64),
                Operator = version
            };
        }
    }
}
