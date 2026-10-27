using System.Globalization;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;

public sealed class NativeOperationExecutorTests
{
    static readonly Uuid Probe = Uuid.Parse("00000000-0000-4000-8000-0000000000b1", CultureInfo.InvariantCulture);
    static readonly Uuid Logical = Uuid.Parse("00000000-0000-4000-8000-0000000000c1", CultureInfo.InvariantCulture);
    static readonly ActorReference Actor = new("member", "m1", "Member One");
    static readonly DateTimeOffset At = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
    static readonly EventStreamAddress Owner = AcceptanceProbe.StreamOf(Probe);
    static readonly ImmutableContentRef Body = new(Uuid.CreateVersion4(), new string('b', 64), 2, "application/json",
        "unit.v1");

    [Fact]
    public async Task ShouldReturnOriginalResponseGivenExactRetryAfterLaterEdit()
    {
        // Arrange
        var (provider, events, _) = Build();
        var first = await ExecuteAsync(provider, Provenance(), 200, "first");
        await EditAsync(provider, "edited later");
        var before = await CountAsync(events);

        // Act
        var retry = await ExecuteAsync(provider, Provenance(At.AddMinutes(5)), 500, "retry candidate");

        // Assert
        var committed = Assert.IsType<AcceptedAppendResult.Committed>(first.Value);
        var replayed = Assert.IsType<AcceptedAppendResult.Replayed>(retry.Value);
        Assert.Equal(committed.Response, replayed.Response);
        Assert.Equal(200, replayed.Response.HttpStatus);
        Assert.Equal(committed.FirstFact, replayed.FirstFact);
        Assert.Equal(before, await CountAsync(events));
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("kind")]
    [InlineData("episode")]
    [InlineData("digest")]
    [InlineData("revision")]
    [InlineData("affected")]
    [InlineData("counter")]
    public async Task ShouldConflictWithoutAppendGivenSameLogicalIdWithChangedComparisonField(string field)
    {
        // Arrange
        var (provider, events, kv) = Build();
        await ExecuteAsync(provider, Provenance(), 200, "first");
        var before = await CountAsync(events);
        var content = kv.Snapshot(ContentRoute).Count;
        var other = new EventStreamAddress(AcceptanceProbe.Realm, "probes", "other");
        var changed = field switch
        {
            "actor" => Provenance() with { Actor = new ActorReference("member", "m2", "Member Two") },
            "kind" => Provenance() with { CommandKind = "Revise" },
            "episode" => Provenance() with { Episode = "second" },
            "digest" => Provenance() with { AuthoredIntentSha256 = new string('e', 64) },
            "revision" => Provenance() with { OriginalExpectedRevision = 1 },
            "affected" => Provenance() with { AffectedOwner = other },
            _ => Provenance() with { ExpectedRevisionOwner = other },
        };

        // Act
        var result = await ExecuteAsync(provider, changed, 200, "second");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.IsType<AcceptedAppendResult.Conflict>(result.Value);
        Assert.Equal(before, await CountAsync(events));
        Assert.Equal(content, kv.Snapshot(ContentRoute).Count);
    }

    [Fact]
    public async Task ShouldReturnWinnersResponseGivenEqualIntentLosesAppendRace()
    {
        // Arrange
        ServiceProvider? competitorProvider = null;
        var (provider, events, _) = Build(inner => new RaceOnceExecutor(inner, async () =>
        {
            // The identical concurrent request commits its own candidate between our hydration and our append.
            await using var scope = competitorProvider!.CreateAsyncScope();
            var winner = new NativeOperationExecutor(scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
                inner, scope.ServiceProvider.GetRequiredService<IImmutableContentStore>());
            await winner.ExecuteAsync(Operation(Provenance(), 200, "winner"), Context(), CancellationToken.None);
        }));
        competitorProvider = provider;

        // Act
        var loser = await ExecuteAsync(provider, Provenance(), 200, "loser");

        // Assert
        var replayed = Assert.IsType<AcceptedAppendResult.Replayed>(loser.Value);
        Assert.Equal("winner", Assert.IsType<StandardResponseBody>(replayed.Response.Body).Content.Schema);
        Assert.Equal(1, await CountAsync(events));
    }

    [Fact]
    public async Task ShouldReturnConflictGivenDifferentIntentCommitsBeforeExecutorHydration()
    {
        // Arrange
        ServiceProvider? competitorProvider = null;
        var (provider, events, _) = Build(inner => new CompeteBeforeHydrationExecutor(inner, async () =>
        {
            await using var scope = competitorProvider!.CreateAsyncScope();
            var winner = new NativeOperationExecutor(scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
                inner, scope.ServiceProvider.GetRequiredService<IImmutableContentStore>());
            await winner.ExecuteAsync(Operation(Provenance(), 200, "winner"), Context(), CancellationToken.None);
        }));
        competitorProvider = provider;
        var different = Provenance() with { AuthoredIntentSha256 = new string('e', 64) };

        // Act
        var result = await ExecuteAsync(provider, different, 200, "loser");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.IsType<AcceptedAppendResult.Conflict>(result.Value);
        Assert.Equal(1, await CountAsync(events));
    }

    [Fact]
    public async Task ShouldReplayGivenGuardFailureAfterConcurrentAcceptance()
    {
        // Arrange
        ServiceProvider? competitorProvider = null;
        var (provider, events, _) = Build(inner => new CompeteBeforeHydrationExecutor(inner, async () =>
        {
            await using var scope = competitorProvider!.CreateAsyncScope();
            var winner = new NativeOperationExecutor(scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
                inner, scope.ServiceProvider.GetRequiredService<IImmutableContentStore>());
            await winner.ExecuteAsync(Operation(Provenance(), 200, "winner"), Context(), CancellationToken.None);
        }, reject: true));
        competitorProvider = provider;

        // Act
        var result = await ExecuteAsync(provider, Provenance(), 200, "loser");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.IsType<AcceptedAppendResult.Replayed>(result.Value);
        Assert.Equal(1, await CountAsync(events));
    }

    [Fact]
    public async Task ShouldReplayGivenEqualIntentCommitsOnLastAppendAttempt()
    {
        // Arrange
        ServiceProvider? competitorProvider = null;
        var (provider, events, _) = Build(inner => new AlwaysConflictExecutor(async () =>
        {
            await using var scope = competitorProvider!.CreateAsyncScope();
            var winner = new NativeOperationExecutor(scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
                inner, scope.ServiceProvider.GetRequiredService<IImmutableContentStore>());
            await winner.ExecuteAsync(Operation(Provenance(), 200, "winner"), Context(), CancellationToken.None);
        }));
        competitorProvider = provider;

        // Act
        var result = await ExecuteAsync(provider, Provenance(), 200, "loser");

        // Assert
        Assert.IsType<AcceptedAppendResult.Replayed>(result.Value);
        Assert.Equal(1, await CountAsync(events));
    }

    [Fact]
    public async Task ShouldResumeByLogicalIdWithoutDuplicateAppendGivenUnknownAppendOutcome()
    {
        // Arrange
        var (provider, events, _) = Build(inner => new CommitThenFailOnceExecutor(inner));

        // Act
        var result = await ExecuteAsync(provider, Provenance(), 200, "first");

        // Assert
        var replayed = Assert.IsType<AcceptedAppendResult.Replayed>(result.Value);
        Assert.Equal(200, replayed.Response.HttpStatus);
        Assert.Equal(1, await CountAsync(events));
    }

    [Fact]
    public async Task ShouldSurfaceConcurrencyGivenEveryAttemptLosesTheRace()
    {
        // Arrange
        var (provider, events, _) = Build(inner => new AlwaysConflictExecutor());

        // Act
        var failure = await Record.ExceptionAsync(() => ExecuteAsync(provider, Provenance(), 200, "first").AsTask());

        // Assert
        Assert.IsType<EventStreamConcurrencyException>(failure);
        Assert.Equal(0, await CountAsync(events));
    }

    [Fact]
    public async Task ShouldRejectWithoutAppendOrFrozenContentGivenGuardFailure()
    {
        // Arrange
        var (provider, events, kv) = Build();
        await EditAsync(provider, "moved on");

        // Act
        var result = await ExecuteAsync(provider, Provenance(), 200, "first", expectedRevision: 0);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal(1, await CountAsync(events));
        Assert.Empty(kv.Snapshot(ContentRoute));
    }

    [Fact]
    public async Task ShouldFailClosedWithoutAppendGivenCorruptFirstResponseContent()
    {
        // Arrange
        var (provider, events, _) = Build(content: store => new CorruptReadStore(store));
        await ExecuteAsync(provider, Provenance(), 200, "first");

        // Act
        var failure = await Record.ExceptionAsync(() => ExecuteAsync(provider, Provenance(), 200, "retry").AsTask());

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.Equal(1, await CountAsync(events));
    }

    [Fact]
    public async Task ShouldReportExclusiveAppendFrontierGivenCommittedBatch()
    {
        // Arrange
        var (provider, _, _) = Build();
        await EditAsync(provider, "one");
        await EditAsync(provider, "two");

        // Act
        var result = await ExecuteAsync(provider, Provenance() with { OriginalExpectedRevision = 2 }, 202, "first",
            expectedRevision: 2);

        // Assert
        var committed = Assert.IsType<AcceptedAppendResult.Committed>(result.Value);
        Assert.Equal(3UL, committed.NextPhysicalPositionExclusive);
        Assert.Equal(3UL, committed.FirstFact.AggregateVersion);
        Assert.Equal(Owner, committed.FirstFact.Stream);
        Assert.Equal(202, committed.Response.HttpStatus);
    }

    [Theory]
    [InlineData("read")]
    [InlineData("write")]
    [InlineData("freeze")]
    public async Task ShouldReplayOriginalTypedResponseGivenEqualWinnerCommitsBeforeImmutablePreparationThrows(string stage)
    {
        // Arrange
        await using var state = await PreparationRaceAsync(stage,
            new ImmutableContentUnavailableException("The losing immutable preparation is unavailable."));

        // Act
        var result = await state.Loser.ExecuteAsync(state.Operation, Context(), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);
        var replay = Assert.IsType<AcceptedAppendResult.Replayed>(result.Value);
        var winner = Assert.IsType<AcceptedAppendResult.Committed>(state.Winner());
        var body = Assert.IsType<StandardResponseBody>(replay.Response.Body);
        var typed = await state.Content.ReadJsonAsync<OperationAcceptedResponse>(Owner.Realm, body.Content);
        var exact = await state.Content.ReadVerifiedAsync(Owner.Realm, body.Content);

        // Assert
        Assert.Equal(winner.Response, replay.Response);
        Assert.Equal(winner.FirstResponse, replay.FirstResponse);
        Assert.Equal(winner.FirstFact, replay.FirstFact);
        Assert.Equal(202, replay.Response.HttpStatus);
        Assert.Equal(new OperationAcceptedResponse(Logical, "/winner", At), typed);
        Assert.Equal((await state.Content.ReadVerifiedAsync(Owner.Realm, state.WinnerBody)).ToArray(), exact.ToArray());
        Assert.Equal(1, await CountAsync(state.Events));
        Assert.Equal(0, state.Transport.Classifications);
        Assert.Same(replay, state.Transport.Observed);
    }

    [Theory]
    [InlineData("read", true)]
    [InlineData("read", false)]
    [InlineData("write", true)]
    [InlineData("write", false)]
    [InlineData("freeze", true)]
    [InlineData("freeze", false)]
    public async Task ShouldPreserveTypedPreparationFailureGivenNoConcurrentWinner(string stage, bool unavailable)
    {
        // Arrange
        var fault = unavailable
            ? new ImmutableContentUnavailableException("The original preparation is not visible yet.")
            : new ImmutableContentIntegrityException("The original preparation cannot be verified.");
        await using var state = await PreparationRaceAsync(stage, fault, commitWinner: false);

        // Act
        var result = await state.Loser.ExecuteAsync(state.Operation, Context(), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(unavailable, result.Error!.IsTransient);
        Assert.Same(fault, state.Transport.Classified);
        Assert.Equal(1, state.Transport.Classifications);
        Assert.Null(state.Transport.Observed);
        Assert.Equal(2, state.Reader.Calls);
        Assert.Equal(0, await CountAsync(state.Events));
    }

    [Theory]
    [InlineData("read")]
    [InlineData("write")]
    [InlineData("freeze")]
    public async Task ShouldRethrowOriginalExpectedFailureGivenNoWinnerAndNoTransportClassifier(string stage)
    {
        // Arrange
        var fault = new ImmutableContentIntegrityException("Exact original failure object.");
        await using var state = await PreparationRaceAsync(stage, fault, commitWinner: false, observeFailures: false);

        // Act
        var failure = await Record.ExceptionAsync(() => state.Loser.ExecuteAsync(state.Operation, Context(),
            CancellationToken.None).AsTask());

        // Assert
        Assert.Same(fault, failure);
        Assert.Equal(2, state.Reader.Calls);
        Assert.Equal(0, await CountAsync(state.Events));
    }

    [Theory]
    [InlineData("read")]
    [InlineData("freeze")]
    public async Task ShouldReturnConflictGivenDifferentIntentWinnerAndThrownPreparationFailure(string stage)
    {
        // Arrange
        await using var state = await PreparationRaceAsync(stage,
            new ImmutableContentUnavailableException("Losing preparation."), changedIntent: true);

        // Act
        var result = await state.Loser.ExecuteAsync(state.Operation, Context(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.IsType<AcceptedAppendResult.Conflict>(result.Value);
        Assert.Equal(0, state.Transport.Classifications);
        Assert.Null(state.Transport.Observed);
        Assert.Equal(2, state.Reader.Calls);
        Assert.Equal(1, await CountAsync(state.Events));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldPassRequestedCancellationWithoutRecheckGivenExpectedPreparationFailure(bool winner)
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        await using var state = await PreparationRaceAsync("read", new ImmutableContentUnavailableException("Losing read."),
            commitWinner: winner, beforeFailure: cancellation.Cancel);

        // Act
        var failure = await Record.ExceptionAsync(() => state.Loser.ExecuteAsync(state.Operation, Context(),
            cancellation.Token).AsTask());

        // Assert
        var canceled = Assert.IsAssignableFrom<OperationCanceledException>(failure);
        Assert.Equal(cancellation.Token, canceled.CancellationToken);
        Assert.Equal(1, state.Reader.Calls);
        Assert.Equal(0, state.Transport.Classifications);
        Assert.Null(state.Transport.Observed);
        Assert.Equal(winner ? 1 : 0, await CountAsync(state.Events));
    }

    [Fact]
    public async Task ShouldPreserveProgrammerFaultWithoutRecheckGivenConcurrentWinnerDuringPreparation()
    {
        // Arrange
        var fault = new InvalidOperationException("A programmer fault is not a source-content failure.");
        await using var state = await PreparationRaceAsync("read", fault);

        // Act
        var failure = await Record.ExceptionAsync(() => state.Loser.ExecuteAsync(state.Operation, Context(),
            CancellationToken.None).AsTask());

        // Assert
        Assert.Same(fault, failure);
        Assert.Equal(1, state.Reader.Calls);
        Assert.Equal(0, state.Transport.Classifications);
        Assert.Null(state.Transport.Observed);
        Assert.Equal(1, await CountAsync(state.Events));
    }

    [Fact]
    public async Task ShouldPreserveUnrequestedCancellationWithoutRecheckGivenConcurrentWinnerDuringPreparation()
    {
        // Arrange
        var fault = new OperationCanceledException("The prepare operation canceled itself.");
        await using var state = await PreparationRaceAsync("write", fault);

        // Act
        var failure = await Record.ExceptionAsync(() => state.Loser.ExecuteAsync(state.Operation, Context(),
            CancellationToken.None).AsTask());

        // Assert
        Assert.Same(fault, failure);
        Assert.Equal(1, state.Reader.Calls);
        Assert.Equal(0, state.Transport.Classifications);
        Assert.Null(state.Transport.Observed);
        Assert.Equal(1, await CountAsync(state.Events));
    }

    static async Task<PreparationRace> PreparationRaceAsync(string stage, Exception failure,
        bool commitWinner = true, bool changedIntent = false, Action? beforeFailure = null, bool observeFailures = true)
    {
        var (provider, events, _) = Build();
        var content = provider.GetRequiredService<IImmutableContentStore>();
        var winnerBody = await content.WriteJsonAsync(Owner.Realm, Uuid.CreateVersion4(), "winner-response.v1",
            new OperationAcceptedResponse(Logical, "/winner", At));
        var candidate = await content.WriteJsonAsync(Owner.Realm, Uuid.CreateVersion4(), "loser-response.v1",
            new OperationAcceptedResponse(Logical, "/loser", At.AddHours(1)));
        var source = await content.WriteJsonAsync(Owner.Realm, Uuid.CreateVersion4(), "prepare-source.v1", new UnitResult());
        AcceptedAppendResult? accepted = null;
        async Task CompeteAsync()
        {
            if (commitWinner)
            {
                await using var scope = provider.CreateAsyncScope();
                var binding = changedIntent ? Provenance() with { AuthoredIntentSha256 = new string('e', 64) } : Provenance();
                var operation = Operation(binding, 202, "winner") with
                {
                    Prepare = (_, _) => ValueTask.FromResult(Result<FirstResponseDescriptor>.Success(new(202,
                        "application/json", winnerBody.Schema, new StandardResponseBody(winnerBody))))
                };
                var native = new NativeOperationExecutor(scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
                    scope.ServiceProvider.GetRequiredService<IAggregateExecutor>(), content);
                var result = await native.ExecuteAsync(operation, Context(), CancellationToken.None);
                Assert.True(result.IsSuccess, result.Error?.Message);
                accepted = result.Value;
            }
            beforeFailure?.Invoke();
        }
        var faulty = new PreparationFaultStore(content, stage, failure, CompeteAsync);
        var transport = new PreparationTransport();
        // Retain the real execution scope until this test's returned race state is disposed.
        var execution = provider.CreateAsyncScope();
        var observedReader = new PreparationReader(execution.ServiceProvider.GetRequiredService<IAggregateReader>());
        var loser = new NativeOperationExecutor(observedReader,
            execution.ServiceProvider.GetRequiredService<IAggregateExecutor>(), faulty, observeFailures ? transport : null);
        var losing = Operation(Provenance(), 202, "loser") with
        {
            Prepare = async (_, ct) =>
            {
                if (stage == "read")
                    await faulty.ReadVerifiedAsync(Owner.Realm, source, ct);
                else if (stage == "write")
                    await faulty.WriteJsonAsync(Owner.Realm, Uuid.CreateVersion4(), "prepare-source.v1", new UnitResult(), ct);
                return Result<FirstResponseDescriptor>.Success(new(202, "application/json", candidate.Schema,
                    new StandardResponseBody(candidate)));
            }
        };
        return new(provider, events, content, winnerBody, loser, losing, () => accepted, transport, execution, observedReader);
    }

    sealed record PreparationRace(ServiceProvider Provider, InMemoryEventStore Events, IImmutableContentStore Content,
        ImmutableContentRef WinnerBody, NativeOperationExecutor Loser, NativeOperation<AcceptanceProbe> Operation,
        Func<AcceptedAppendResult?> Winner, PreparationTransport Transport, AsyncServiceScope Execution, PreparationReader Reader) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await Execution.DisposeAsync();
            await Provider.DisposeAsync();
        }
    }

    sealed class PreparationTransport : INativeOperationTransportObserver
    {
        public int Classifications { get; private set; }
        public ImmutableContentIntegrityException? Classified { get; private set; }
        public AcceptedAppendResult? Observed { get; private set; }
        public RequestError? ClassifyFailure(AcceptedOperationBinding binding, IExecutionContext context,
            ImmutableContentIntegrityException exception)
        {
            Classifications++;
            Classified = exception;
            return new RequestError(RequestErrorKind.Conflict, exception.Message,
                exception is ImmutableContentUnavailableException);
        }
        public void Observe(AcceptedOperationBinding binding, IExecutionContext context, AcceptedAppendResult accepted) =>
            Observed = accepted;
    }

    sealed class PreparationReader(IAggregateReader inner) : IAggregateReader
    {
        public int Calls { get; private set; }
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
            where TAggregate : Aggregate
        {
            Calls++;
            return inner.HydrateAsync(aggregate, ct);
        }
    }

    sealed class PreparationFaultStore(IImmutableContentStore inner, string stage, Exception failure,
        Func<Task> beforeFailure) : IImmutableContentStore
    {
        public async Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            if (stage == "write" && schema == "prepare-source.v1" || stage == "freeze" && schema == FirstResponses.Schema)
            {
                await beforeFailure();
                throw failure;
            }
            return await inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        }
        public async Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default)
        {
            if (stage == "read" && reference.Schema == "prepare-source.v1")
            {
                await beforeFailure();
                throw failure;
            }
            return await inner.ReadVerifiedAsync(realm, reference, ct);
        }
    }

    const string ContentRoute = "kv://bdgrz/immutable-content/tenant-a-";

    static OperationProvenance Provenance(DateTimeOffset? acceptedAt = null) => new(Logical, Logical, null, "Admit",
        Actor, Owner, "initial", 0, new string('d', 64), acceptedAt ?? At, Owner, Owner);

    static NativeOperation<AcceptanceProbe> Operation(OperationProvenance provenance, int status, string payload,
        ulong expectedRevision = 0) => new(AcceptedOperationBinding.Of(provenance),
        () => new AcceptanceProbe(Probe, Logical), probe => probe.Acceptance,
        probe => probe.Version == expectedRevision
            ? Result<FirstResponseDescriptor>.Success(new FirstResponseDescriptor(status, "application/json", "unit.v1",
                new StandardResponseBody(Body with { Schema = payload })))
            : Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The probe revision changed.", false)),
        (probe, frozen) => probe.Admit(provenance, frozen, payload, expectedRevision));

    static async ValueTask<Result<AcceptedAppendResult>> ExecuteAsync(ServiceProvider provider,
        OperationProvenance provenance, int status, string payload, ulong expectedRevision = 0)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<NativeOperationExecutor>()
            .ExecuteAsync(Operation(provenance, status, payload, expectedRevision), Context(), CancellationToken.None);
    }

    static async Task EditAsync(ServiceProvider provider, string payload)
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IAggregateExecutor>().ExecuteAsync(
            new AcceptanceProbe(Probe), probe => AggregateOutcome.CommitOnSuccess(probe.Edit(payload)), Context(),
            CancellationToken.None);
        Assert.True(result.IsSuccess);
    }

    static async Task<int> CountAsync(InMemoryEventStore events)
    {
        var count = 0;
        try
        {
            await foreach (var _ in events.ReadAsync(Owner, 0, CancellationToken.None))
                count++;
        }
        catch (InvalidOperationException)
        {
            // An unknown stream reads as empty.
        }

        return count;
    }

    static RequestDispatchContext Context() => new(RequestActor.System);

    static (ServiceProvider Provider, InMemoryEventStore Events, InMemoryKvClient Kv) Build(
        Func<IAggregateExecutor, IAggregateExecutor>? executor = null,
        Func<IImmutableContentStore, IImmutableContentStore>? content = null)
    {
        var services = new ServiceCollection();
        var events = new InMemoryEventStore();
        var kv = new InMemoryKvClient();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddPortia();
        IImmutableContentStore store = new FitzKvImmutableContentStore(new PersistenceKvClient(kv));
        services.AddSingleton(content?.Invoke(store) ?? store);
        services.AddScoped(provider => new NativeOperationExecutor(provider.GetRequiredService<IAggregateReader>(),
            executor?.Invoke(provider.GetRequiredService<IAggregateExecutor>()) ??
            provider.GetRequiredService<IAggregateExecutor>(), provider.GetRequiredService<IImmutableContentStore>()));
        return (services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true }), events, kv);
    }

    /// <summary>Runs a competing commit, then reports the first attempt as a lost append race.</summary>
    sealed class RaceOnceExecutor(IAggregateExecutor inner, Func<Task> competitor) : IAggregateExecutor
    {
        int _calls;

        public ValueTask<Result> ExecuteAsync<TAggregate>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome> operation, IExecutionContext context, CancellationToken ct)
            where TAggregate : Aggregate => inner.ExecuteAsync(aggregate, operation, context, ct);

        public async ValueTask<Result<TOut>> ExecuteAsync<TAggregate, TOut>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome<TOut>> operation, IExecutionContext context, CancellationToken ct)
            where TAggregate : Aggregate
        {
            if (Interlocked.Increment(ref _calls) == 1)
            {
                await competitor();
                throw new EventStreamConcurrencyException("Lost the append race.", new InvalidOperationException());
            }

            return await inner.ExecuteAsync(aggregate, operation, context, ct);
        }
    }

    /// <summary>Lets another operation commit before the real executor hydrates, without forcing an OCC error.</summary>
    sealed class CompeteBeforeHydrationExecutor(IAggregateExecutor inner, Func<Task> competitor, bool reject = false)
        : IAggregateExecutor
    {
        int _calls;

        public ValueTask<Result> ExecuteAsync<TAggregate>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome> operation, IExecutionContext context, CancellationToken ct)
            where TAggregate : Aggregate => inner.ExecuteAsync(aggregate, operation, context, ct);

        public async ValueTask<Result<TOut>> ExecuteAsync<TAggregate, TOut>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome<TOut>> operation, IExecutionContext context, CancellationToken ct)
            where TAggregate : Aggregate
        {
            if (Interlocked.Increment(ref _calls) == 1)
            {
                await competitor();
                if (reject)
                    return Result<TOut>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The guard rejected the stale candidate.", false));
            }
            return await inner.ExecuteAsync(aggregate, operation, context, ct);
        }
    }

    /// <summary>Commits the first attempt but reports it as unknown, as a timeout after append would.</summary>
    sealed class CommitThenFailOnceExecutor(IAggregateExecutor inner) : IAggregateExecutor
    {
        int _calls;

        public ValueTask<Result> ExecuteAsync<TAggregate>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome> operation, IExecutionContext context, CancellationToken ct)
            where TAggregate : Aggregate => inner.ExecuteAsync(aggregate, operation, context, ct);

        public async ValueTask<Result<TOut>> ExecuteAsync<TAggregate, TOut>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome<TOut>> operation, IExecutionContext context, CancellationToken ct)
            where TAggregate : Aggregate
        {
            var result = await inner.ExecuteAsync(aggregate, operation, context, ct);
            if (Interlocked.Increment(ref _calls) == 1)
                throw new EventStreamConcurrencyException("Append outcome unknown.", new TimeoutException());
            return result;
        }
    }

    sealed class AlwaysConflictExecutor(Func<Task>? finalCompetitor = null) : IAggregateExecutor
    {
        int _calls;

        public ValueTask<Result> ExecuteAsync<TAggregate>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome> operation, IExecutionContext context, CancellationToken ct)
            where TAggregate : Aggregate =>
            throw new EventStreamConcurrencyException("Lost the append race.", new InvalidOperationException());

        public async ValueTask<Result<TOut>> ExecuteAsync<TAggregate, TOut>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome<TOut>> operation, IExecutionContext context, CancellationToken ct)
            where TAggregate : Aggregate
        {
            if (Interlocked.Increment(ref _calls) == NativeOperationExecutor.MaximumAttempts && finalCompetitor is not null)
                await finalCompetitor();
            throw new EventStreamConcurrencyException("Lost the append race.", new InvalidOperationException());
        }
    }

    /// <summary>Writes normally but fails every verified read, as corrupt or missing content does.</summary>
    sealed class CorruptReadStore(IImmutableContentStore inner) : IImmutableContentStore
    {
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);

        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default) =>
            throw new ImmutableContentIntegrityException("The stored content does not match its reference.");
    }
}
