using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Readers;
using Bdgrz.Compliance.Features.Criteria.Services;
using Bdgrz.Compliance.Features.Kernel.Services;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class SourceCriteriaOperationReaderTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldReturnExactTerminalDraftOutcomeGivenQualifiedCommittedOwner(bool accepted)
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var owner = new CriteriaEdition(id, operation);
        var actor = new ActorAttribution("operator", "issuer");
        var request = new DraftCriteriaEdition(id, operation, "tsc", "edition_1");
        Assert.True(owner.Draft(operation, "tsc", "edition_1", actor, Admission(owner, operation, request, actor)).IsSuccess);
        if (accepted)
        {
            Assert.True(owner.ActivateDraft(operation, 1, Uuid.CreateVersion4()).IsSuccess);
            Assert.True(owner.CompleteDraft(operation, 1, Uuid.CreateVersion4()).IsSuccess);
        }
        else
            Assert.True(owner.RecordInitialLabelRefusal(operation, Uuid.CreateVersion4(), "label_taken").IsSuccess);
        var facts = new AggregateScenario<CriteriaEdition>(owner).PendingEvents.ToArray();
        var reader = new SourceCriteriaOperationReader(new ReplayReader(facts), editionHead: new EditionHead(true));

        // Act
        var result = await reader.GetOperationAsync(id, operation, null);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(accepted ? "accepted" : "refused", result.Value!.Status);
        Assert.Equal(accepted ? null : "label_taken", result.Value.Reason);
    }

    [Fact]
    public async Task ShouldReturnPendingGivenTerminalOwnerHeadCannotBeQualified()
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var owner = new CriteriaEdition(id, operation);
        var actor = new ActorAttribution("operator", "issuer");
        var request = new DraftCriteriaEdition(id, operation, "tsc", "edition_1");
        Assert.True(owner.Draft(operation, "tsc", "edition_1", actor, Admission(owner, operation, request, actor)).IsSuccess);
        Assert.True(owner.RecordInitialLabelRefusal(operation, Uuid.CreateVersion4(), "label_taken").IsSuccess);
        var reader = new SourceCriteriaOperationReader(
            new ReplayReader(new AggregateScenario<CriteriaEdition>(owner).PendingEvents.ToArray()), editionHead: new EditionHead(false));

        // Act
        var result = await reader.GetOperationAsync(id, operation, null);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("pending", result.Value!.Status);
    }

    [Fact]
    public async Task ShouldDistinguishCancellationFromCancelledDraftGivenCommittedFacts()
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var draftId = Uuid.CreateVersion4();
        var cancelId = Uuid.CreateVersion4();
        var actor = new ActorAttribution("operator", "issuer");
        var draft = new DraftCriteriaEdition(id, draftId, "tsc", "edition_1");
        var initial = new CriteriaEdition(id, draftId);
        Assert.True(initial.Draft(draftId, "tsc", "edition_1", actor, Admission(initial, draftId, draft, actor)).IsSuccess);
        var cancelling = new CriteriaEdition(id, cancelId);
        new AggregateScenario<CriteriaEdition>(cancelling).Given(new AggregateScenario<CriteriaEdition>(initial).PendingEvents.ToArray());
        var cancellation = new CancelCriteriaEditionDraftRequest(id, cancelId, draftId);
        Assert.True(cancelling.CancelDraftRequest(cancelId, draftId, actor,
            Admission(cancelling, cancelId, cancellation, actor)).IsSuccess);
        var facts = new AggregateScenario<CriteriaEdition>(initial).PendingEvents
            .Concat(new AggregateScenario<CriteriaEdition>(cancelling).PendingEvents).ToArray();
        var reader = new SourceCriteriaOperationReader(new ReplayReader(facts), editionHead: new EditionHead(true));

        // Act
        var cancelled = await reader.GetOperationAsync(id, draftId, null);
        var cancellationReceipt = await reader.GetOperationAsync(id, cancelId, null);

        // Assert
        Assert.Equal("refused", cancelled.Value!.Status);
        Assert.Equal("cancelled", cancelled.Value.Reason);
        Assert.Equal("accepted", cancellationReceipt.Value!.Status);
    }

    static AccessAdmissionEvidence Admission<T>(CriteriaEdition owner, Uuid operationId, T request, ActorAttribution actor)
        where T : notnull
    {
        var identity = new AccessAdmissionSourceVersion("stream://bdgrz/identities/actor", "generation", 1);
        var policy = new AccessAdmissionSourceVersion("stream://bdgrz/policies/current", "generation", 1);
        return new AccessAdmissionEvidence(Uuid.CreateVersion4(), actor.Subject, actor.Issuer, operationId,
            owner.Stream.ToString(), CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(request), 1),
            new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero), "policy-v1", identity, policy)
        {
            FormatVersion = 1,
            PolicyContentSha256 = new string('a', 64),
            Operator = new AccessAdmissionSourceVersion("stream://bdgrz/operators/actor", "generation", 1),
        };
    }

    sealed class ReplayReader(DomainEvent[] events) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
            where TAggregate : Aggregate
        {
            ct.ThrowIfCancellationRequested();
            new AggregateScenario<CriteriaEdition>((CriteriaEdition)(object)aggregate).Given(events);
            return ValueTask.FromResult(aggregate);
        }
    }
    [Fact]
    public async Task ShouldReturnPendingGivenOperationNotObserved()
    {
        // Arrange
        var editionId = Uuid.CreateVersion4();
        var operationId = Uuid.CreateVersion4();
        var reader = new SourceCriteriaOperationReader(new EmptyAggregateReader());

        // Act
        var result = await reader.GetOperationAsync(editionId, operationId, null);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(editionId, result.Value.EditionId);
        Assert.Equal(operationId, result.Value.OperationId);
        Assert.Equal("pending", result.Value.Status);
        Assert.Null(result.Value.Reason);
    }

    [Fact]
    public async Task ShouldReturnPendingGivenEntryOperationNotObserved()
    {
        // Arrange
        var editionId = Uuid.CreateVersion4();
        var operationId = Uuid.CreateVersion4();
        var reader = new SourceCriteriaOperationReader(new EmptyAggregateReader());

        // Act
        var result = await reader.GetOperationAsync(editionId, operationId, "CC6.1");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("pending", result.Value.Status);
    }

    [Fact]
    public async Task ShouldRejectEmptyIdentityGivenOperationRead()
    {
        // Arrange
        var aggregateReader = new EmptyAggregateReader();
        var reader = new SourceCriteriaOperationReader(aggregateReader);

        // Act
        var result = await reader.GetOperationAsync(Uuid.Empty, Uuid.CreateVersion4(), null);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error.Kind);
        Assert.Equal(0, aggregateReader.ReadCount);
    }

    [Fact]
    public async Task ShouldReturnUnavailableGivenDraftHeadProviderIsMissing()
    {
        // Arrange
        var reader = new SourceCriteriaOperationReader(new EmptyAggregateReader());

        // Act
        var result = await reader.GetDraftAsync(Uuid.CreateVersion4());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.Error!.IsTransient);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldUseOnlyQualifiedDraftHeadGivenNoObservedEdition(bool qualified)
    {
        // Arrange
        var reader = new SourceCriteriaOperationReader(new EmptyAggregateReader(), editionHead: new EditionHead(qualified));

        // Act
        var result = await reader.GetDraftAsync(Uuid.CreateVersion4());

        // Assert
        Assert.Equal(qualified, result.IsSuccess);
        if (qualified)
            Assert.Equal("pending", result.Value!.Status);
        else
            Assert.True(result.Error!.IsTransient);
    }

    sealed class EditionHead(bool qualified) : ICriteriaFreshEditionHead
    {
        public ValueTask<bool> IsCurrentAsync(EventStreamAddress stream, Uuid aggregateId, ulong nextResourceOffset,
            CancellationToken ct = default) => ValueTask.FromResult(qualified);
    }

    sealed class EmptyAggregateReader : IAggregateReader
    {
        public int ReadCount { get; private set; }

        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
            where TAggregate : Aggregate
        {
            ct.ThrowIfCancellationRequested();
            ReadCount++;
            return ValueTask.FromResult(aggregate);
        }
    }
}
