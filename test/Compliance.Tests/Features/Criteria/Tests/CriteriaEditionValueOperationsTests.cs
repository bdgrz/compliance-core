using System.Runtime.CompilerServices;
using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaEditionValueOperationsTests
{
    static readonly Uuid User = Uuid.CreateVersion4();
    static readonly Uuid Edition = Uuid.CreateVersion4();
    static readonly Uuid Draft = Uuid.CreateVersion4();
    static readonly ClaimsPrincipal Actor = new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", User.ToString())], "session"));

    [Fact]
    public async Task ShouldFailClosedGivenMissingFreshHeadProvider()
    {
        // Arrange
        var operations = new CriteriaEditionValueOperations(null!, null!, null!);

        // Act
        var result = await operations.SetFieldAsync(Context(Field()), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.True(result.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldRejectValueWritesBeforeAnyAppendGivenNegativeReadiness()
    {
        // Arrange
        var reader = new MemoryReader();
        var writer = new MemoryExecutor(reader);
        var operations = new CriteriaEditionValueOperations(reader, writer, new MemorySource(reader),
            new Authority(), new Head(true), readiness: new TestCriteriaAuthoringReadiness(false));
        var disclosure = new SetCriteriaEditionDisclosure(Edition, Draft, "privacy", "lifecycle", Uuid.CreateVersion4(), 0, "Unsupported.");

        // Act
        var field = await operations.SetFieldAsync(Context(Field()), CancellationToken.None);
        var note = await operations.SetDisclosureAsync(new RequestContext<SetCriteriaEditionDisclosure>(disclosure, Actor), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, field.Error?.Kind);
        Assert.True(field.Error?.IsTransient);
        Assert.Equal(RequestErrorKind.Conflict, note.Error?.Kind);
        Assert.True(note.Error?.IsTransient);
        Assert.Equal(0, writer.Executions);
    }

    [Fact]
    public async Task ShouldRemainPendingGivenReadinessLostAfterValueClaim()
    {
        // Arrange
        var reader = new MemoryReader();
        var writer = new MemoryExecutor(reader);
        var operations = new CriteriaEditionValueOperations(reader, writer, new MemorySource(reader),
            new Authority(), new Head(true), readiness: new SequencedCriteriaAuthoringReadiness(true, true, false));

        // Act
        var result = await operations.SetFieldAsync(Context(Field()), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("pending", result.Value!.Status);
        Assert.Equal(1, writer.Executions);
    }

    [Fact]
    public async Task ShouldFailRetryablyGivenReadinessLostBeforeValueClaim()
    {
        // Arrange
        var reader = new MemoryReader();
        var writer = new MemoryExecutor(reader);
        var operations = new CriteriaEditionValueOperations(reader, writer, new MemorySource(reader),
            new Authority(), new Head(true), readiness: new SequencedCriteriaAuthoringReadiness(true, false));

        // Act
        var result = await operations.SetFieldAsync(Context(Field()), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(result.Error?.IsTransient);
        Assert.Equal(0, writer.Executions);
    }

    [Fact]
    public async Task ShouldRefuseInvalidShapeBeforeClaimingAnOperation()
    {
        // Arrange
        var reader = new MemoryReader();
        var writer = new MemoryExecutor(reader);
        var operations = Create(reader, writer, new MemorySource(reader), new Head(true));

        // Act
        var result = await operations.SetFieldAsync(Context(Field() with { TextValue = new string('x', 2001) }), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Equal(0, writer.Executions);
    }

    [Fact]
    public async Task ShouldNotWriteGivenAnUnqualifiedTargetHead()
    {
        // Arrange
        var reader = new MemoryReader();
        var writer = new MemoryExecutor(reader);
        var operations = Create(reader, writer, new MemorySource(reader), new Head(false));

        // Act
        var result = await operations.SetFieldAsync(Context(Field()), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.True(result.Error.IsTransient);
        Assert.Equal(0, writer.Executions);
    }

    [Fact]
    public async Task ShouldReturnRetryableUnavailableWithoutClaimGivenAdmissionIsUnavailable()
    {
        // Arrange
        var reader = new MemoryReader();
        var writer = new MemoryExecutor(reader);
        var operations = new CriteriaEditionValueOperations(reader, writer, new MemorySource(reader),
            new Authority { Unavailable = true }, new Head(true), readiness: TestCriteriaAuthoringReadiness.Ready);

        // Act
        var result = await operations.SetFieldAsync(Context(Field()), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.True(result.Error.IsTransient);
        Assert.Equal(0, writer.Executions);
    }

    [Theory]
    [InlineData(false, "tsc")]
    [InlineData(true, "other")]
    public async Task ShouldRemainPendingWithoutClaimGivenUnobservedOrWrongFrameworkDraft(bool draftExists, string framework)
    {
        // Arrange
        var reader = new MemoryReader();
        var writer = new MemoryExecutor(reader);
        var source = new MemorySource(reader) { DraftExists = draftExists, Framework = framework };
        var operations = Create(reader, writer, source, new Head(true));

        // Act
        var result = await operations.SetFieldAsync(Context(Field()), CancellationToken.None);

        // Assert
        Assert.Equal("pending", result.Value!.Status);
        Assert.Equal(0, writer.Executions);
    }

    [Fact]
    public async Task ShouldRecordClaimThenProposalGivenQualifiedSourcesAndLeaveOutcomePending()
    {
        // Arrange
        var request = Field();
        var reader = new MemoryReader();
        var writer = new MemoryExecutor(reader);
        var operations = Create(reader, writer, new MemorySource(reader), new Head(true));

        // Act
        var result = await operations.SetFieldAsync(Context(request), CancellationToken.None);

        // Assert
        Assert.Equal("pending", result.Value!.Status);
        Assert.Equal(2, writer.Executions);
        var claim = CriteriaEditionValueOperation.AddressOf(Edition, "field", request.Field, null, null, request.OperationId);
        var target = CriteriaEditionValue.AddressOf(Edition, "field", request.Field, null, null);
        var claimed = Assert.IsType<CriteriaEditionValueOperationClaimed>(Assert.Single(reader.Read(claim)));
        var proposed = Assert.IsType<CriteriaEditionValueProposed>(Assert.Single(reader.Read(target)));
        Assert.Equal(claimed.Metadata.EventId, proposed.OperationClaimEventId);
    }

    [Fact]
    public async Task ShouldRecordOneDisclosureGivenQualifiedSources()
    {
        // Arrange
        var request = new SetCriteriaEditionDisclosure(Edition, Draft, "privacy", "lifecycle",
            Uuid.CreateVersion4(), 0, "Unsupported.");
        var reader = new MemoryReader();
        var writer = new MemoryExecutor(reader);
        var operations = Create(reader, writer, new MemorySource(reader), new Head(true));

        // Act
        var result = await operations.SetDisclosureAsync(new RequestContext<SetCriteriaEditionDisclosure>(request, Actor), CancellationToken.None);

        // Assert
        Assert.Equal("pending", result.Value!.Status);
        var address = CriteriaEditionValue.AddressOf(Edition, "disclosure", null, request.Category, request.Code);
        var proposal = Assert.IsType<CriteriaEditionValueProposed>(Assert.Single(reader.Read(address)));
        Assert.Equal("Unsupported.", proposal.Intent.TextValue);
    }

    [Fact]
    public async Task ShouldRemainPendingGivenAmbiguousClaimAppend()
    {
        // Arrange
        var reader = new MemoryReader();
        var writer = new MemoryExecutor(reader) { Ambiguous = true };
        var operations = Create(reader, writer, new MemorySource(reader), new Head(true));

        // Act
        var result = await operations.SetFieldAsync(Context(Field()), CancellationToken.None);

        // Assert
        Assert.Equal("pending", result.Value!.Status);
        Assert.Equal(1, writer.Executions);
    }

    [Fact]
    public async Task ShouldNotUseAnUnverifiedOutcomeAsTheBasisForACorrection()
    {
        // Arrange
        var request = Field();
        var reader = new MemoryReader();
        var writer = new MemoryExecutor(reader);
        var operations = Create(reader, writer, new MemorySource(reader), new Head(true));
        Assert.True((await operations.SetFieldAsync(Context(request), CancellationToken.None)).IsSuccess);
        var address = CriteriaEditionValue.AddressOf(Edition, "field", request.Field, null, null);
        var proposal = Assert.IsType<CriteriaEditionValueProposed>(Assert.Single(reader.Read(address)));
        var evidence = new CriteriaEditionValueDecisionEvidence("draft", Uuid.CreateVersion4(), 1, 2, 10);
        var accepted = DomainEventSeed.Attach(new CriteriaEditionValueAccepted(Edition, request.OperationId, 1,
            proposal.Metadata.EventId, evidence), proposal.Metadata.AggregateId, 2);
        reader.Append(address, [accepted]);
        var correction = request with { OperationId = Uuid.CreateVersion4(), ExpectedRevision = 1, TextValue = "Updated." };

        // Act
        var result = await operations.SetFieldAsync(Context(correction), CancellationToken.None);

        // Assert
        Assert.Equal("pending", result.Value!.Status);
        Assert.Equal(2, writer.Executions);
    }

    [Fact]
    public async Task ShouldRejectWrongUserEvidenceBeforeWriting()
    {
        // Arrange
        var reader = new MemoryReader();
        var writer = new MemoryExecutor(reader);
        var operations = new CriteriaEditionValueOperations(reader, writer, new MemorySource(reader),
            new Authority { WrongUser = true }, new Head(true), readiness: TestCriteriaAuthoringReadiness.Ready);

        // Act
        var result = await operations.SetFieldAsync(Context(Field()), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error!.Kind);
        Assert.Equal(0, writer.Executions);
    }

    static SetCriteriaEditionField Field() => new(Edition, Draft, "coverage_note", Uuid.CreateVersion4(), 0, "Partial.", null);
    static RequestContext<SetCriteriaEditionField> Context(SetCriteriaEditionField request) => new(request, Actor);
    static CriteriaEditionValueOperations Create(MemoryReader reader, MemoryExecutor writer, MemorySource source, Head head) =>
        new(reader, writer, source, new Authority(), head, readiness: TestCriteriaAuthoringReadiness.Ready);

    sealed class MemoryReader : IAggregateReader
    {
        readonly Dictionary<EventStreamAddress, List<DomainEvent>> _events = [];
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
            where TAggregate : Aggregate
        {
            new AggregateScenario<TAggregate>(aggregate).Given(Read(aggregate.Stream));
            return ValueTask.FromResult(aggregate);
        }
        public DomainEvent[] Read(EventStreamAddress stream) => _events.TryGetValue(stream, out var items) ? items.ToArray() : [];
        public void Append(EventStreamAddress stream, IEnumerable<DomainEvent> events)
        {
            if (!_events.TryGetValue(stream, out var items))
                _events.Add(stream, items = []);
            items.AddRange(events);
        }
    }

    sealed class MemoryExecutor(MemoryReader reader) : IAggregateExecutor
    {
        public int Executions { get; private set; }
        public bool Ambiguous { get; init; }
        public async ValueTask<Result> ExecuteAsync<TAggregate>(TAggregate aggregate, Func<TAggregate, AggregateOutcome> operation,
            IExecutionContext context, CancellationToken ct = default) where TAggregate : Aggregate
        {
            Executions++;
            var owner = await reader.HydrateAsync(aggregate, ct);
            var result = operation(owner).Result;
            if (Ambiguous)
                throw new IOException("Append acknowledgement unknown.");
            if (result.IsSuccess)
                reader.Append(owner.Stream, new AggregateScenario<TAggregate>(owner).PendingEvents);
            return result;
        }
        public ValueTask<Result<TOut>> ExecuteAsync<TAggregate, TOut>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome<TOut>> operation, IExecutionContext context, CancellationToken ct = default)
            where TAggregate : Aggregate => throw new NotSupportedException();
    }

    sealed class MemorySource(MemoryReader reader) : ICriteriaExactSourceReader
    {
        public bool DraftExists { get; init; } = true;
        public string Framework { get; init; } = "tsc";
        public ValueTask<CriteriaOrderedSourceEvent?> FindAsync(EventStreamAddress stream, Uuid aggregateId,
            Uuid eventId, CancellationToken ct = default) => ValueTask.FromResult<CriteriaOrderedSourceEvent?>(null);
        public async IAsyncEnumerable<CriteriaOrderedSourceEvent> ReadAsync(EventStreamAddress stream, Uuid aggregateId,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            if (aggregateId == new CriteriaEdition(Edition).Id)
            {
                if (DraftExists)
                {
                    var request = new CriteriaEditionDraftRequested(Edition, Draft, Draft, 1, Framework, "edition_1",
                        new ActorAttribution(User.ToString(), "bdgrz"), Authority.Evidence("stream://bdgrz/example/target", Draft, new string('a', 64)), null, null);
                    request.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), aggregateId, 1, DateTimeOffset.UtcNow));
                    var drafted = new CriteriaEditionDrafted(Edition, Draft, Draft, Framework, "edition_1", 1, Uuid.CreateVersion4());
                    drafted.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), aggregateId, 2, DateTimeOffset.UtcNow));
                    yield return new CriteriaOrderedSourceEvent(stream, 0, 1, request);
                    yield return new CriteriaOrderedSourceEvent(stream, 1, 2, drafted);
                }
                yield break;
            }
            var items = reader.Read(stream);
            var baseArea = stream.Resource.StartsWith("edition-value-operation-", StringComparison.Ordinal) ? 3UL : 10UL;
            for (var index = 0; index < items.Length; index++)
            {
                ct.ThrowIfCancellationRequested();
                yield return new CriteriaOrderedSourceEvent(stream, (ulong)index, baseArea + (ulong)index, items[index]);
            }
            await Task.CompletedTask;
        }
    }

    sealed class Head(bool current) : ICriteriaFreshEntryHead
    {
        public ValueTask<bool> IsCurrentAsync(EventStreamAddress stream, Uuid aggregateId, ulong nextResourceOffset,
            CancellationToken ct = default) => ValueTask.FromResult(current);
    }

    sealed class Authority : IAccessAdmissionAuthority
    {
        public bool WrongUser { get; init; }
        public bool Unavailable { get; init; }
        public ValueTask<AccessAdmissionEvidence?> AdmitCriteriaOperatorAsync(IRequestContext actor, Uuid operationId,
            string target, string intentSha256, CancellationToken ct = default)
        {
            if (Unavailable)
                throw new AccessAdmissionUnavailableException();
            var result = Evidence(target, operationId, intentSha256);
            return ValueTask.FromResult<AccessAdmissionEvidence?>(WrongUser ? result with { UserId = Uuid.CreateVersion4() } : result);
        }
        public ValueTask<AccessAdmissionEvidence?> AdmitProgramCreationAsync(IRequestContext actor, Uuid tenantId,
            Uuid programId, string target, string intentSha256, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask<AccessAdmissionEvidence?> AdmitProgramSelectionAsync(IRequestContext actor, Uuid tenantId,
            Uuid programId, Uuid operationId, string target, string intentSha256, CancellationToken ct = default) => throw new NotSupportedException();
        public static AccessAdmissionEvidence Evidence(string target, Uuid operationId, string digest)
        {
            var source = new AccessAdmissionSourceVersion("stream://bdgrz/authority/source", "v1", 1);
            return new AccessAdmissionEvidence(User, User.ToString(), "bdgrz", operationId, target, digest,
                new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero), "policy", source, source)
            {
                FormatVersion = 1,
                PolicyContentSha256 = new string('a', 64),
                Operator = source
            };
        }
    }
}
