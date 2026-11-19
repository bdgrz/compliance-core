using System.Runtime.CompilerServices;
using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Readers;
using Bdgrz.Compliance.Features.Criteria.Services;
using Bdgrz.Compliance.Features.Kernel.Services;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaEntryOperationsTests
{
    static readonly Uuid UserId = Uuid.CreateVersion4();
    static readonly Uuid EditionId = Uuid.CreateVersion4();
    static readonly Uuid DraftGeneration = Uuid.CreateVersion4();
    static readonly ClaimsPrincipal Actor = new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", UserId.ToString())], "session"));

    [Fact]
    public async Task ShouldReturnUnavailableGivenNoQualifiedHeadProvider()
    {
        // Arrange
        var operations = new CriteriaEntryOperations(null!, null!, null!, null!);

        // Act
        var result = await operations.ProposeAsync(Context(Create(Uuid.CreateVersion4())), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(result.Error?.IsTransient);
    }

    [Fact]
    public async Task ShouldRejectEntryWritesBeforeAnyAppendGivenNegativeReadiness()
    {
        // Arrange
        var aggregates = new MemoryReader([]);
        var executor = new MemoryExecutor(aggregates);
        var authority = new Authority();
        var operations = new CriteriaEntryOperations(aggregates, executor, new MemorySource([], aggregates),
            new Receipts(), authority, new Head(true), readiness: new TestCriteriaAuthoringReadiness(false));
        var summary = new CorrectCriterionSummary(EditionId, "CC6.1", Uuid.CreateVersion4(), 1, "summary");

        // Act
        var change = await operations.ProposeAsync(Context(Create(Uuid.CreateVersion4())), CancellationToken.None);
        var correction = await operations.CorrectSummaryAsync(new RequestContext<CorrectCriterionSummary>(summary, Actor), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, change.Error?.Kind);
        Assert.True(change.Error?.IsTransient);
        Assert.Equal(RequestErrorKind.Conflict, correction.Error?.Kind);
        Assert.True(correction.Error?.IsTransient);
        Assert.Equal(0, executor.Executions);
        Assert.Equal(0, authority.Calls);
    }

    [Fact]
    public async Task ShouldRemainPendingGivenReadinessLostAfterClaimAndBeforeTargetAppend()
    {
        // Arrange
        var aggregates = new MemoryReader([]);
        var executor = new MemoryExecutor(aggregates);
        var operations = new CriteriaEntryOperations(aggregates, executor, new MemorySource([], aggregates),
            new Receipts(), new Authority(), new Head(true), readiness: new SequencedCriteriaAuthoringReadiness(true, true, false));

        // Act
        var result = await operations.ProposeAsync(Context(Create(Uuid.CreateVersion4())), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("pending", result.Value.Status);
        Assert.Equal(1, executor.Executions);
    }

    [Fact]
    public async Task ShouldFailRetryablyGivenReadinessLostBeforeClaimAppend()
    {
        // Arrange
        var aggregates = new MemoryReader([]);
        var executor = new MemoryExecutor(aggregates);
        var operations = new CriteriaEntryOperations(aggregates, executor, new MemorySource([], aggregates),
            new Receipts(), new Authority(), new Head(true), readiness: new SequencedCriteriaAuthoringReadiness(true, false));

        // Act
        var result = await operations.ProposeAsync(Context(Create(Uuid.CreateVersion4())), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(result.Error?.IsTransient);
        Assert.Equal(0, executor.Executions);
    }

    [Fact]
    public async Task ShouldReturnRetryableUnavailableBeforeAnyAppendGivenAdmissionUnavailable()
    {
        // Arrange
        var aggregates = new MemoryReader([]);
        var executor = new MemoryExecutor(aggregates);
        var operations = new CriteriaEntryOperations(aggregates, executor, new MemorySource([], aggregates),
            new Receipts(), new Authority { Unavailable = true }, new Head(true), readiness: TestCriteriaAuthoringReadiness.Ready);

        // Act
        var result = await operations.ProposeAsync(Context(Create(Uuid.CreateVersion4())), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(result.Error?.IsTransient);
        Assert.Equal(0, executor.Executions);
    }

    [Fact]
    public async Task ShouldNotExecuteGivenUnqualifiedCurrentHead()
    {
        // Arrange
        var aggregates = new MemoryReader([]);
        var executor = new MemoryExecutor(aggregates);
        var authority = new Authority();
        var operations = new CriteriaEntryOperations(aggregates, executor, new MemorySource([], aggregates), new Receipts(),
            authority, new Head(false), readiness: TestCriteriaAuthoringReadiness.Ready);

        // Act
        var result = await operations.ProposeAsync(Context(Create(Uuid.CreateVersion4())), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(result.Error?.IsTransient);
        Assert.Equal(0, executor.Executions);
        Assert.Equal(1, authority.Calls);
    }

    [Fact]
    public async Task ShouldRecordPendingProposalGivenQualifiedCurrentHeadAndCurrentAdmission()
    {
        // Arrange
        var request = Create(Uuid.CreateVersion4());
        var aggregates = new MemoryReader([]);
        var executor = new MemoryExecutor(aggregates);
        var authority = new Authority();
        var head = new Head(true);
        var operations = new CriteriaEntryOperations(aggregates, executor, new MemorySource([], aggregates), new Receipts(),
            authority, head, readiness: TestCriteriaAuthoringReadiness.Ready);

        // Act
        var result = await operations.ProposeAsync(Context(request), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("pending", result.Value.Status);
        Assert.Equal(2, executor.Executions);
        Assert.Equal(1UL, head.LastPosition);
        var intent = new CriterionChangeIntent(EditionId, DraftGeneration, "CC6.1", request.OperationId, 0,
            "create", "criterion", "security", null, null, "A product summary.");
        Assert.Equal(CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(intent), 1), authority.LastDigest);
        Assert.Equal(CriteriaEntry.StreamFor(EditionId, "CC6.1").ToString(), authority.LastTarget);
    }

    [Fact]
    public async Task ShouldNotRecordPointOfFocusGivenUnobservedParentAcceptance()
    {
        // Arrange
        var request = Create(Uuid.CreateVersion4()) with
        {
            Identifier = "CC6.1-pof-1",
            Kind = "point_of_focus",
            ParentIdentifier = "CC6.1",
            ParentIncarnationId = Uuid.CreateVersion4(),
        };
        var aggregates = new MemoryReader([]);
        var executor = new MemoryExecutor(aggregates);
        var operations = new CriteriaEntryOperations(aggregates, executor, new MemorySource([], aggregates), new Receipts(),
            new Authority(), new Head(true), readiness: TestCriteriaAuthoringReadiness.Ready);

        // Act
        var result = await operations.ProposeAsync(Context(request), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("pending", result.Value.Status);
        Assert.Equal(0, executor.Executions);
    }

    [Fact]
    public async Task ShouldNotBindEntryGivenUnobservedEditionDraft()
    {
        // Arrange
        var aggregates = new MemoryReader([]);
        var executor = new MemoryExecutor(aggregates);
        var source = new MemorySource([], aggregates) { DraftExists = false };
        var operations = new CriteriaEntryOperations(aggregates, executor, source, new Receipts(),
            new Authority(), new Head(true), readiness: TestCriteriaAuthoringReadiness.Ready);

        // Act
        var result = await operations.ProposeAsync(Context(Create(Uuid.CreateVersion4())), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("pending", result.Value.Status);
        Assert.Equal(0, executor.Executions);
    }

    [Fact]
    public async Task ShouldRejectMissingParentGivenInvalidPointOfFocusWithoutClaimingAnOperation()
    {
        // Arrange
        var operationId = Uuid.CreateVersion4();
        var request = Create(operationId) with
        {
            Kind = "point_of_focus",
            ParentIdentifier = null,
            ParentIncarnationId = null
        };
        var aggregates = new MemoryReader([]);
        var executor = new MemoryExecutor(aggregates);
        var operations = new CriteriaEntryOperations(aggregates, executor, new MemorySource([], aggregates),
            new Receipts(), new Authority(), new Head(true), readiness: TestCriteriaAuthoringReadiness.Ready);

        // Act
        var result = await operations.ProposeAsync(Context(request), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(aggregates.Read(CriteriaEntryOperation.AddressOf(EditionId, request.Identifier, operationId)));
        Assert.Equal(0, executor.Executions);
    }

    [Fact]
    public async Task ShouldKeepStateRefusalPendingGivenAConcurrentClaimMayStillWin()
    {
        // Arrange
        var operationId = Uuid.CreateVersion4();
        var request = Create(operationId) with { ExpectedRevision = 9 };
        var aggregates = new MemoryReader([]);
        var executor = new MemoryExecutor(aggregates);
        var operations = new CriteriaEntryOperations(aggregates, executor, new MemorySource([], aggregates),
            new Receipts(), new Authority(), new Head(true), readiness: TestCriteriaAuthoringReadiness.Ready);

        // Act
        var result = await operations.ProposeAsync(Context(request), CancellationToken.None);

        // Another same-ID attempt can still claim after the absent read.
        var intent = new CriterionChangeIntent(EditionId, DraftGeneration, request.Identifier, operationId,
            request.ExpectedRevision, request.Action, request.Kind, request.Category, request.ParentIdentifier,
            request.ParentIncarnationId, request.Summary);
        var actor = new ActorAttribution(UserId.ToString(), "bdgrz");
        var other = new CriteriaEntryOperation(EditionId, request.Identifier, operationId);
        var admission = Authority.Evidence(CriteriaEntry.StreamFor(EditionId, request.Identifier).ToString(), operationId,
            CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(intent), 1));
        Assert.True(other.Claim(intent, 1, actor, admission).IsSuccess);
        aggregates.Append(other.Stream, new AggregateScenario<CriteriaEntryOperation>(other).PendingEvents);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("pending", result.Value.Status);
        Assert.IsType<CriterionEntryOperationClaimed>(Assert.Single(aggregates.Read(other.Stream)));
        Assert.Equal(0, executor.Executions);
    }

    [Fact]
    public async Task ShouldRejectEvidenceGivenDifferentAuthenticatedUser()
    {
        // Arrange
        var aggregates = new MemoryReader([]);
        var executor = new MemoryExecutor(aggregates);
        var operations = new CriteriaEntryOperations(aggregates, executor, new MemorySource([], aggregates), new Receipts(),
            new Authority { WrongUser = true }, new Head(true), readiness: TestCriteriaAuthoringReadiness.Ready);

        // Act
        var result = await operations.ProposeAsync(Context(Create(Uuid.CreateVersion4())), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(0, executor.Executions);
    }

    [Fact]
    public async Task ShouldReturnPendingGivenAmbiguousAppendFailure()
    {
        // Arrange
        var aggregates = new MemoryReader([]);
        var executor = new MemoryExecutor(aggregates) { FailAppend = true };
        var receipts = new Receipts { Status = "accepted" };
        var operations = new CriteriaEntryOperations(aggregates, executor, new MemorySource([], aggregates), receipts,
            new Authority(), new Head(true), readiness: TestCriteriaAuthoringReadiness.Ready);

        // Act
        var result = await operations.ProposeAsync(Context(Create(Uuid.CreateVersion4())), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("pending", result.Value.Status);
        Assert.Equal(0, receipts.Calls);
    }

    [Fact]
    public async Task ShouldReturnAcceptedGivenDurableSummaryNoChangeOutcome()
    {
        // Arrange
        var creationId = Uuid.CreateVersion4();
        var owner = new CriteriaEntry(EditionId, "CC6.1", creationId);
        var intent = new CriterionChangeIntent(EditionId, DraftGeneration, "CC6.1", creationId, 0,
            "create", "criterion", "security", null, null, "A product summary.");
        var actor = new ActorAttribution(UserId.ToString(), "bdgrz");
        var evidence = Authority.Evidence(owner.Stream.ToString(), creationId,
            CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(intent), 1));
        Assert.True(Propose(owner, intent, actor, evidence).IsSuccess);
        var proposal = Assert.IsType<CriterionChangeProposed>(Assert.Single(new AggregateScenario<CriteriaEntry>(owner).PendingEvents));
        Assert.True(owner.Decide(creationId, 1, proposal.Metadata.EventId, true, null, DraftEvidence()).IsSuccess);
        var history = new AggregateScenario<CriteriaEntry>(owner).PendingEvents.ToArray();
        var aggregates = new MemoryReader(history);
        var receipts = new Receipts { Status = "accepted" };
        var operations = new CriteriaEntryOperations(aggregates, new MemoryExecutor(aggregates),
            new MemorySource(history, aggregates), receipts, new Authority(), new Head(true), readiness: TestCriteriaAuthoringReadiness.Ready);
        var request = new CorrectCriterionSummary(EditionId, "CC6.1", Uuid.CreateVersion4(), 1, "A product summary.");

        // Act
        var result = await operations.CorrectSummaryAsync(new RequestContext<CorrectCriterionSummary>(request, Actor),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("accepted", result.Value.Status);
        Assert.Equal(1, receipts.Calls);
        Assert.IsType<CriterionSummaryUnchanged>(Assert.Single(aggregates.Read(owner.Stream)));
    }

    [Fact]
    public async Task ShouldRejectStructuralReuseGivenAPendingNoChangeSummaryClaim()
    {
        // Arrange
        var creationId = Uuid.CreateVersion4();
        var owner = new CriteriaEntry(EditionId, "CC6.1", creationId);
        var original = new CriterionChangeIntent(EditionId, DraftGeneration, "CC6.1", creationId, 0,
            "create", "criterion", "security", null, null, "A product summary.");
        var actor = new ActorAttribution(UserId.ToString(), "bdgrz");
        var evidence = Authority.Evidence(owner.Stream.ToString(), creationId,
            CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(original), 1));
        Assert.True(Propose(owner, original, actor, evidence).IsSuccess);
        var proposal = Assert.IsType<CriterionChangeProposed>(Assert.Single(new AggregateScenario<CriteriaEntry>(owner).PendingEvents));
        Assert.True(owner.Decide(creationId, 1, proposal.Metadata.EventId, true, null, DraftEvidence()).IsSuccess);
        var history = new AggregateScenario<CriteriaEntry>(owner).PendingEvents.ToArray();
        var aggregates = new MemoryReader(history);
        var executor = new MemoryExecutor(aggregates);
        var operations = new CriteriaEntryOperations(aggregates, executor, new MemorySource(history, aggregates),
            new Receipts(), new Authority(), new Head(true), readiness: TestCriteriaAuthoringReadiness.Ready);
        var operationId = Uuid.CreateVersion4();
        var summary = new CorrectCriterionSummary(EditionId, "CC6.1", operationId, 1, "A product summary.");
        var structural = Create(operationId) with
        {
            ExpectedRevision = 1,
            Action = "remove",
            Kind = null,
            Category = null,
            Summary = null
        };

        // Act
        var noChange = await operations.CorrectSummaryAsync(new RequestContext<CorrectCriterionSummary>(summary, Actor),
            CancellationToken.None);
        var reuse = await operations.ProposeAsync(Context(structural), CancellationToken.None);

        // Assert
        Assert.True(noChange.IsSuccess);
        Assert.Equal("pending", noChange.Value.Status);
        Assert.Equal(RequestErrorKind.Conflict, reuse.Error?.Kind);
        var claimStream = CriteriaEntryOperation.AddressOf(EditionId, "CC6.1", operationId);
        Assert.IsType<CriterionEntryOperationClaimed>(Assert.Single(aggregates.Read(claimStream)));
    }

    [Fact]
    public async Task ShouldRecoverExactNoChangeGivenAppliedButUnacknowledgedTargetAppend()
    {
        // Arrange
        var creationId = Uuid.CreateVersion4();
        var owner = new CriteriaEntry(EditionId, "CC6.1", creationId);
        var original = new CriterionChangeIntent(EditionId, DraftGeneration, "CC6.1", creationId, 0,
            "create", "criterion", "security", null, null, "A product summary.");
        var actor = new ActorAttribution(UserId.ToString(), "bdgrz");
        var evidence = Authority.Evidence(owner.Stream.ToString(), creationId,
            CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(original), 1));
        Assert.True(Propose(owner, original, actor, evidence).IsSuccess);
        var proposal = Assert.IsType<CriterionChangeProposed>(Assert.Single(new AggregateScenario<CriteriaEntry>(owner).PendingEvents));
        Assert.True(owner.Decide(creationId, 1, proposal.Metadata.EventId, true, null, DraftEvidence()).IsSuccess);
        var history = new AggregateScenario<CriteriaEntry>(owner).PendingEvents.ToArray();
        var aggregates = new MemoryReader(history);
        var source = new MemorySource(history, aggregates);
        var receipts = new Receipts { Status = "accepted" };
        var request = new CorrectCriterionSummary(EditionId, "CC6.1", Uuid.CreateVersion4(), 1, "A product summary.");
        var first = new CriteriaEntryOperations(aggregates, new MemoryExecutor(aggregates) { FailTargetAppendAfterWrite = true },
            source, receipts, new Authority(), new Head(true), readiness: TestCriteriaAuthoringReadiness.Ready);
        var retry = new CriteriaEntryOperations(aggregates, new MemoryExecutor(aggregates), source, receipts, new Authority(), new Head(true), readiness: TestCriteriaAuthoringReadiness.Ready);

        // Act
        var unknown = await first.CorrectSummaryAsync(new RequestContext<CorrectCriterionSummary>(request, Actor), CancellationToken.None);
        var recovered = await retry.CorrectSummaryAsync(new RequestContext<CorrectCriterionSummary>(request, Actor), CancellationToken.None);
        var changed = await retry.CorrectSummaryAsync(new RequestContext<CorrectCriterionSummary>(request with { Summary = "Different." }, Actor), CancellationToken.None);

        // Assert
        Assert.True(unknown.IsSuccess);
        Assert.Equal("pending", unknown.Value.Status);
        Assert.True(recovered.IsSuccess);
        Assert.Equal("accepted", recovered.Value.Status);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.IsType<CriterionSummaryUnchanged>(Assert.Single(aggregates.Read(owner.Stream)));
    }

    [Fact]
    public async Task ShouldRecoverStaleSummaryRetryGivenTheClaimedIntentIsExact()
    {
        // Arrange
        var fixture = StaleSummaryFixture();
        var receipts = new Receipts { Status = "refused" };
        var operations = new CriteriaEntryOperations(fixture.Reader, new MemoryExecutor(fixture.Reader), fixture.Source,
            receipts, new Authority(), new Head(true), readiness: TestCriteriaAuthoringReadiness.Ready);

        // Act
        var first = await operations.CorrectSummaryAsync(new RequestContext<CorrectCriterionSummary>(fixture.Request, Actor), CancellationToken.None);
        var retry = await operations.CorrectSummaryAsync(new RequestContext<CorrectCriterionSummary>(fixture.Request, Actor), CancellationToken.None);
        var changed = await operations.CorrectSummaryAsync(new RequestContext<CorrectCriterionSummary>(fixture.Request with { Summary = "Changed" }, Actor), CancellationToken.None);

        // Assert
        Assert.True(first.IsSuccess);
        Assert.Equal("refused", first.Value.Status);
        Assert.True(retry.IsSuccess);
        Assert.Equal("refused", retry.Value.Status);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        var outcome = Assert.IsType<CriterionSummaryRefused>(Assert.Single(fixture.Reader.Read(fixture.Target.Stream)));
        Assert.Equal(1, outcome.ObservedRevision);
    }

    [Fact]
    public async Task ShouldRemainPendingGivenTargetAppendRacesAStaleSummaryRefusal()
    {
        // Arrange
        var fixture = StaleSummaryFixture();
        var concurrentOperation = Uuid.CreateVersion4();
        var concurrentIntent = new CriterionSummaryIntent(EditionId, "CC6.1", concurrentOperation, 1, "A product summary.");
        var actor = new ActorAttribution(UserId.ToString(), "bdgrz");
        var admission = Authority.Evidence(fixture.Target.Stream.ToString(), concurrentOperation,
            CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(concurrentIntent), 1));
        var concurrent = new CriterionSummaryUnchanged(concurrentIntent, 1, actor, admission, Uuid.CreateVersion4());
        concurrent.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), fixture.Target.Id, 3, DateTimeOffset.UtcNow));
        var executor = new MemoryExecutor(fixture.Reader) { BeforeTargetHydration = () => fixture.Reader.Append(fixture.Target.Stream, [concurrent]) };
        var operations = new CriteriaEntryOperations(fixture.Reader, executor, fixture.Source, new Receipts { Status = "refused" },
            new Authority(), new Head(true), readiness: TestCriteriaAuthoringReadiness.Ready);

        // Act
        var result = await operations.CorrectSummaryAsync(new RequestContext<CorrectCriterionSummary>(fixture.Request, Actor), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("pending", result.Value.Status);
        Assert.Same(concurrent, Assert.Single(fixture.Reader.Read(fixture.Target.Stream)));
    }

    static (MemoryReader Reader, MemorySource Source, CriteriaEntry Target, CorrectCriterionSummary Request) StaleSummaryFixture()
    {
        var creationId = Uuid.CreateVersion4();
        var target = new CriteriaEntry(EditionId, "CC6.1", creationId);
        var original = new CriterionChangeIntent(EditionId, DraftGeneration, "CC6.1", creationId, 0,
            "create", "criterion", "security", null, null, "A product summary.");
        var actor = new ActorAttribution(UserId.ToString(), "bdgrz");
        var evidence = Authority.Evidence(target.Stream.ToString(), creationId,
            CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(original), 1));
        Assert.True(Propose(target, original, actor, evidence).IsSuccess);
        var proposal = Assert.IsType<CriterionChangeProposed>(Assert.Single(new AggregateScenario<CriteriaEntry>(target).PendingEvents));
        Assert.True(target.Decide(creationId, 1, proposal.Metadata.EventId, true, null, DraftEvidence()).IsSuccess);
        var history = new AggregateScenario<CriteriaEntry>(target).PendingEvents.ToArray();
        var reader = new MemoryReader(history);
        var request = new CorrectCriterionSummary(EditionId, "CC6.1", Uuid.CreateVersion4(), 0, "A product summary.");
        var intent = new CriterionSummaryIntent(EditionId, "CC6.1", request.OperationId, request.ExpectedRevision, request.Summary);
        var admission = Authority.Evidence(target.Stream.ToString(), request.OperationId,
            CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(intent), 1));
        var operation = new CriteriaEntryOperation(EditionId, "CC6.1", request.OperationId);
        var claim = new CriterionEntryOperationClaimed(null, intent, 1, actor, admission);
        claim.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), operation.Id, 1, DateTimeOffset.UtcNow));
        reader.Append(operation.Stream, [claim]);
        return (reader, new MemorySource(history, reader), target, request);
    }

    [Fact]
    public async Task ShouldReconcileOneClaimGivenAnAppliedButUnacknowledgedClaimAppend()
    {
        // Arrange
        var operationId = Uuid.CreateVersion4();
        var request = Create(operationId);
        var aggregates = new MemoryReader([]);
        var source = new MemorySource([], aggregates);
        var ambiguous = new CriteriaEntryOperations(aggregates,
            new MemoryExecutor(aggregates) { FailAppendAfterWrite = true }, source, new Receipts(),
            new Authority(), new Head(true), readiness: TestCriteriaAuthoringReadiness.Ready);
        var retry = new CriteriaEntryOperations(aggregates, new MemoryExecutor(aggregates), source,
            new Receipts(), new Authority(), new Head(true), readiness: TestCriteriaAuthoringReadiness.Ready);

        // Act
        var unknown = await ambiguous.ProposeAsync(Context(request), CancellationToken.None);
        var recovered = await retry.ProposeAsync(Context(request), CancellationToken.None);

        // Assert
        Assert.True(unknown.IsSuccess);
        Assert.Equal("pending", unknown.Value.Status);
        Assert.True(recovered.IsSuccess);
        Assert.Equal("pending", recovered.Value.Status);
        Assert.Single(aggregates.Read(CriteriaEntryOperation.AddressOf(EditionId, "CC6.1", operationId)));
    }

    [Fact]
    public async Task ShouldRejectChangedIntentGivenPreviouslyRecordedOperation()
    {
        // Arrange
        var operationId = Uuid.CreateVersion4();
        var owner = new CriteriaEntry(EditionId, "CC6.1", operationId);
        var intent = new CriterionChangeIntent(EditionId, DraftGeneration, "CC6.1", operationId, 0,
            "create", "criterion", "security", null, null, "A product summary.");
        var actor = new ActorAttribution(UserId.ToString(), "bdgrz");
        var evidence = Authority.Evidence(owner.Stream.ToString(), operationId,
            CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(intent), 1));
        Assert.True(Propose(owner, intent, actor, evidence).IsSuccess);
        var history = new AggregateScenario<CriteriaEntry>(owner).PendingEvents.ToArray();
        var aggregates = new MemoryReader(history);
        var executor = new MemoryExecutor(aggregates);
        var authority = new Authority();
        var operations = new CriteriaEntryOperations(aggregates, executor, new MemorySource(history, aggregates), new Receipts(),
            authority, new Head(true), readiness: TestCriteriaAuthoringReadiness.Ready);

        // Act
        var result = await operations.ProposeAsync(Context(Create(operationId) with { Summary = "Different." }),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.False(result.Error?.IsTransient);
        Assert.Equal(1, authority.Calls);
        Assert.Equal(0, executor.Executions);
    }

    [Fact]
    public async Task ShouldKeepExistingClaimPendingGivenTargetChangedBeforeItsAppend()
    {
        // Arrange
        var operationId = Uuid.CreateVersion4();
        var request = Create(operationId);
        var intent = new CriterionChangeIntent(EditionId, DraftGeneration, "CC6.1", operationId, 0,
            "create", "criterion", "security", null, null, "A product summary.");
        var actor = new ActorAttribution(UserId.ToString(), "bdgrz");
        var claim = new CriteriaEntryOperation(EditionId, "CC6.1", operationId);
        var evidence = Authority.Evidence(CriteriaEntry.StreamFor(EditionId, "CC6.1").ToString(), operationId,
            CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(intent), 1));
        Assert.True(claim.Claim(intent, 1, actor, evidence).IsSuccess);

        var otherId = Uuid.CreateVersion4();
        var otherIntent = intent with { OperationId = otherId };
        var other = new CriteriaEntry(EditionId, "CC6.1", otherId);
        var otherAdmission = Authority.Evidence(other.Stream.ToString(), otherId,
            CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(otherIntent), 1));
        Assert.True(Propose(other, otherIntent, actor, otherAdmission).IsSuccess);
        var proposed = Assert.IsType<CriterionChangeProposed>(Assert.Single(new AggregateScenario<CriteriaEntry>(other).PendingEvents));
        Assert.True(other.Decide(otherId, 1, proposed.Metadata.EventId, true, null, DraftEvidence()).IsSuccess);
        var targetHistory = new AggregateScenario<CriteriaEntry>(other).PendingEvents.ToArray();
        var aggregates = new MemoryReader(targetHistory);
        aggregates.Append(claim.Stream, new AggregateScenario<CriteriaEntryOperation>(claim).PendingEvents);
        var executor = new MemoryExecutor(aggregates);
        var operations = new CriteriaEntryOperations(aggregates, executor, new MemorySource(targetHistory, aggregates),
            new Receipts(), new Authority(), new Head(true), readiness: TestCriteriaAuthoringReadiness.Ready);

        // Act
        var result = await operations.ProposeAsync(Context(request), CancellationToken.None);
        var changed = await operations.ProposeAsync(Context(request with { Summary = "Different." }), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("pending", result.Value.Status);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.False(changed.Error?.IsTransient);
        Assert.Equal(0, executor.Executions);
    }

    static CriteriaEntryDecisionEvidence DraftEvidence() => new("draft", Uuid.CreateVersion4(), 0, 1, 2);

    static Result Propose(CriteriaEntry owner, CriterionChangeIntent intent, ActorAttribution actor,
        AccessAdmissionEvidence evidence)
    {
        var claim = new CriterionEntryOperationClaimed(intent, null, 1, actor, evidence);
        claim.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion5(intent.OperationId, "claim"),
            CriteriaEntryOperation.IdentityOf(intent.EditionId, intent.Identifier, intent.OperationId), 1, DateTimeOffset.UtcNow));
        return owner.Propose(intent, 1, actor, evidence, null, claim);
    }

    static ProposeCriterionChange Create(Uuid operationId) => new(EditionId, DraftGeneration, "CC6.1", operationId,
        0, "create", "criterion", "security", null, null, "A product summary.");
    static RequestContext<ProposeCriterionChange> Context(ProposeCriterionChange request) => new(request, Actor);

    sealed class MemoryReader(DomainEvent[] history) : IAggregateReader
    {
        readonly Dictionary<EventStreamAddress, List<DomainEvent>> _written = [];

        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
            where TAggregate : Aggregate
        {
            ct.ThrowIfCancellationRequested();
            var events = aggregate is CriteriaEntry ? history.Concat(Read(aggregate.Stream)).ToArray() : Read(aggregate.Stream);
            new AggregateScenario<TAggregate>(aggregate).Given(events);
            return ValueTask.FromResult(aggregate);
        }

        public DomainEvent[] Read(EventStreamAddress stream) =>
            _written.TryGetValue(stream, out var events) ? events.ToArray() : [];

        public void Append(EventStreamAddress stream, IEnumerable<DomainEvent> events)
        {
            if (!_written.TryGetValue(stream, out var recorded))
                _written.Add(stream, recorded = []);
            recorded.AddRange(events);
        }
    }

    sealed class MemoryExecutor(MemoryReader reader) : IAggregateExecutor
    {
        public int Executions { get; private set; }
        public bool FailAppend { get; init; }
        public bool FailAppendAfterWrite { get; init; }
        public bool FailTargetAppendAfterWrite { get; init; }
        public Action? BeforeTargetHydration { get; set; }
        public async ValueTask<Result> ExecuteAsync<TAggregate>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome> operation, IExecutionContext context, CancellationToken ct = default)
            where TAggregate : Aggregate
        {
            Executions++;
            if (aggregate is CriteriaEntry && BeforeTargetHydration is { } before)
            {
                BeforeTargetHydration = null;
                before();
            }
            var current = await reader.HydrateAsync(aggregate, ct);
            var result = operation(current).Result;
            if (FailAppend)
                throw new IOException("The append result is unknown.");
            if (result.IsSuccess)
                reader.Append(current.Stream, new AggregateScenario<TAggregate>(current).PendingEvents);
            if (FailAppendAfterWrite || FailTargetAppendAfterWrite && current is CriteriaEntry)
                throw new IOException("The append applied but its acknowledgement was lost.");
            return result;
        }
        public ValueTask<Result<TOut>> ExecuteAsync<TAggregate, TOut>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome<TOut>> operation, IExecutionContext context, CancellationToken ct = default)
            where TAggregate : Aggregate => throw new NotSupportedException();
    }

    sealed class MemorySource(DomainEvent[] history, MemoryReader reader) : ICriteriaExactSourceReader
    {
        public bool DraftExists { get; init; } = true;
        public ValueTask<CriteriaOrderedSourceEvent?> FindAsync(EventStreamAddress stream, Uuid aggregateId,
            Uuid eventId, CancellationToken ct = default) => ValueTask.FromResult<CriteriaOrderedSourceEvent?>(null);

        public async IAsyncEnumerable<CriteriaOrderedSourceEvent> ReadAsync(EventStreamAddress stream, Uuid aggregateId,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            if (aggregateId == new CriteriaEdition(EditionId).Id)
            {
                if (DraftExists)
                {
                    var drafted = new CriteriaEditionDrafted(EditionId, DraftGeneration, Uuid.CreateVersion4(),
                        "tsc", "2017_tsc", 1, Uuid.CreateVersion4());
                    drafted.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), aggregateId, 1, DateTimeOffset.UtcNow));
                    yield return new CriteriaOrderedSourceEvent(stream, 0, 0, drafted);
                }
                yield break;
            }
            var records = stream.Resource.StartsWith("entry-operation-", StringComparison.Ordinal)
                ? reader.Read(stream) : history.Concat(reader.Read(stream)).ToArray();
            for (var index = 0; index < records.Length; index++)
            {
                ct.ThrowIfCancellationRequested();
                yield return new CriteriaOrderedSourceEvent(stream, (ulong)index, (ulong)index, records[index]);
            }
            await Task.CompletedTask;
        }
    }

    sealed class Head(bool current) : ICriteriaFreshEntryHead
    {
        public ulong LastPosition { get; private set; }
        public ValueTask<bool> IsCurrentAsync(EventStreamAddress stream, Uuid aggregateId, ulong nextResourceOffset,
            CancellationToken ct = default)
        {
            LastPosition = nextResourceOffset;
            return ValueTask.FromResult(current);
        }
    }

    sealed class Receipts : ICriteriaOperationReader
    {
        public string Status { get; init; } = "pending";
        public int Calls { get; private set; }
        public ValueTask<Result<CriteriaEditionDraftView>> GetDraftAsync(Uuid editionId, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public ValueTask<Result<CriteriaOperationReceipt>> GetOperationAsync(Uuid editionId, Uuid operationId,
            string? identifier, CancellationToken ct = default)
        {
            Calls++;
            return ValueTask.FromResult(Result<CriteriaOperationReceipt>.Success(new(editionId, operationId, Status)));
        }
    }

    sealed class Authority : IAccessAdmissionAuthority
    {
        public int Calls { get; private set; }
        public bool WrongUser { get; init; }
        public bool Unavailable { get; init; }
        public string? LastDigest { get; private set; }
        public string? LastTarget { get; private set; }
        public ValueTask<AccessAdmissionEvidence?> AdmitCriteriaOperatorAsync(IRequestContext actor, Uuid operationId,
            string target, string intentSha256, CancellationToken ct = default)
        {
            Calls++;
            if (Unavailable)
                throw new AccessAdmissionUnavailableException();
            LastDigest = intentSha256;
            LastTarget = target;
            var evidence = Evidence(target, operationId, intentSha256);
            return ValueTask.FromResult<AccessAdmissionEvidence?>(WrongUser
                ? evidence with { UserId = Uuid.CreateVersion4() }
                : evidence);
        }
        public ValueTask<AccessAdmissionEvidence?> AdmitProgramCreationAsync(IRequestContext actor, Uuid tenantId,
            Uuid programId, string target, string intentSha256, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask<AccessAdmissionEvidence?> AdmitProgramSelectionAsync(IRequestContext actor, Uuid tenantId,
            Uuid programId, Uuid operationId, string target, string intentSha256, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public static AccessAdmissionEvidence Evidence(string target, Uuid operationId, string digest)
        {
            var source = new AccessAdmissionSourceVersion("stream://bdgrz/authority/source", "v1", 1);
            return new AccessAdmissionEvidence(UserId, UserId.ToString(), "bdgrz", operationId, target, digest,
                new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero), "policy", source, source)
            {
                FormatVersion = 1,
                PolicyContentSha256 = new string('a', 64),
                Operator = source
            };
        }
    }
}
