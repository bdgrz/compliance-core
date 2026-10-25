using System.Collections;
using System.Reflection;
using System.Text.Json;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Fitz.Testing;
using Bdgrz.Compliance.Features.Readiness;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Readiness;

public sealed class ReadinessNativeHistoryTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRetainBoundedCommandAuthorityGivenManyHistoricalReadinessWrites(bool gapPlan)
    {
        // Arrange
        await using var provider = ReadinessTestServices.Build();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var executor = scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var gap = Uuid.CreateVersion4();
        var member = Uuid.CreateVersion4();
        for (var index = 0; index < 24; index++)
            if (gapPlan)
            {
                await ReadinessSeed.PlanAsync(reader, executor, content, tenant, program, gap, member,
                    new DateOnly(2027, 1, 1), $"Plan {index}", member, Now.AddMinutes(index));
                await ReadinessSeed.AnnotateAsync(reader, executor, content, tenant, program, Uuid.CreateVersion4(),
                    gap, $"Annotation {index}", member, Now.AddMinutes(index));
            }
            else
            {
                var assessment = Uuid.CreateVersion4();
                await ReadinessSeed.AdoptAsync(reader, executor, content, tenant, program, assessment,
                    new ReadinessEvaluation([], [], [], new string('a', 64)), Now, member, Now.AddMinutes(index));
                await ReadinessSeed.DecideAsync(reader, executor, content, tenant, program, assessment,
                    ReadinessDecisionKind.TypeIEntry, ProgramReadiness.Defer, "Review next time.", member,
                    Now.AddMinutes(index));
            }

        // Act
        Aggregate owner = gapPlan
            ? await reader.HydrateAsync(new ReadinessGapPlan(tenant, program, gap))
            : await reader.HydrateAsync(new ProgramReadiness(tenant, program));
        var retained = owner.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Select(field => field.GetValue(owner)).OfType<IEnumerable>()
            .Where(value => value is not string).Sum(value => value.Cast<object>().Count());

        // Assert
        Assert.True(retained <= 8, $"The command owner retained {retained} historical collection entries.");
        Assert.True(owner.Version >= 48);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRefuseUnboundFailureWithoutCancelingDecisionOrReleasingHeldPlanGivenLegacyProof(bool foreignGap)
    {
        // Arrange
        await using var provider = ReadinessTestServices.Build();
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var executor = services.GetRequiredService<IAggregateExecutor>();
        var content = services.GetRequiredService<IImmutableContentStore>();
        var bus = services.GetRequiredService<IRequestBus>();
        var tenant = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var assessment = Uuid.CreateVersion4();
        var runner = Uuid.CreateVersion4();
        var decider = Uuid.CreateVersion4();
        var gap = new ReadinessGapView(Uuid.CreateVersion4(), "missing_input", "risks", "risk_reviewed", "Review risks.", []);
        await ProgramManagementServices.SeedAsync(provider, new ComplianceProgram(tenant, programId), program =>
        {
            Assert.Null(program.Create("SOC 2", new ProgramPlan(null, null, null, null, null, null), runner, "Runner", Now));
            return Result.Success;
        });
        await ReadinessSeed.AdoptAsync(reader, executor, content, tenant, programId, assessment,
            new ReadinessEvaluation([], [], [gap], new string('a', 64)), Now, runner, Now);
        await ReadinessSeed.PlanAsync(reader, executor, content, tenant, programId, gap.GapId, runner,
            new DateOnly(2027, 1, 1), "Close the gap.", runner, Now);
        var request = new DecideReadiness(Uuid.CreateVersion4(), tenant, programId, assessment, 2,
            ProgramReadiness.DoNotProceed, "Continue remediation.");
        var admitted = await bus.DispatchAsync(request, new RequestDispatchContext(ProgramManagementServices.Actor(decider),
            new HttpInvocation("PUT", "/synthetic/readiness", "/synthetic/readiness", "readiness-proof")), CancellationToken.None);
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);
        var record = await ReadinessProcesses.StartDecisionAsync(provider, tenant, programId, request.CommandId);
        var acquire = await ReadinessProcesses.StepAsync(provider, ReadinessEffects.DecisionProcessName, record,
            "acquire", nameof(AuthorizeReadinessGapPlanReadinessDecisionUse), Assert.Single(record.Targets));
        var acquired = await ReadinessProcesses.SendReceiverAsync(provider, acquire!);
        Assert.IsType<ReceiverOutcome.Applied>(acquired.Value);
        var program = await reader.HydrateAsync(new ProgramReadiness(tenant, programId, request.CommandId));
        var accepted = program.PendingDecision!;
        var terms = ProgramReadiness.DecisionTerms(accepted)!.Value;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            program_id = programId.ToString(),
            assessment_id = assessment.ToString(),
            reason = "A failure from an unrelated or earlier decision.",
            gap_id = foreignGap ? Uuid.CreateVersion4().ToString() : null,
        });
        var proof = await content.WriteVerifiedAsync(tenant.ToString(), Uuid.CreateVersion4(),
            "ReadinessDecisionCanceled.FailureProof.v1", "application/json", bytes);
        var command = new AbortReadinessDecision(Uuid.CreateVersion4(), request.CommandId, program.Stream,
            terms.Generation, 0, proof);
        var before = program.CommittedStreamPosition;

        // Act
        var refused = await ReadinessProcesses.SendReceiverAsync(provider, command);
        var after = await reader.HydrateAsync(new ProgramReadiness(tenant, programId, request.CommandId));
        var plan = await reader.HydrateAsync(new ReadinessGapPlan(tenant, programId, gap.GapId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, refused.Error?.Kind);
        Assert.False(refused.Error!.IsTransient);
        Assert.Equal(before, after.CommittedStreamPosition);
        Assert.Equal(accepted.Metadata.EventId, after.PendingDecision!.Metadata.EventId);
        Assert.True(plan.Uses.HasActiveUse);
    }
    [Theory]
    [InlineData("another_admission")]
    [InlineData("another_root")]
    [InlineData("another_generation")]
    [InlineData("another_intent")]
    [InlineData("outside_gap")]
    [InlineData("valid")]
    [InlineData("changed_failure_replay")]
    public async Task ShouldBindCancellationToExactAdmissionAndFrozenGapGivenFailureProof(string mismatch)
    {
        // Arrange
        await using var provider = ReadinessTestServices.Build();
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var executor = services.GetRequiredService<IAggregateExecutor>();
        var content = services.GetRequiredService<IImmutableContentStore>();
        var bus = services.GetRequiredService<IRequestBus>();
        var tenant = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var assessment = Uuid.CreateVersion4();
        var runner = Uuid.CreateVersion4();
        var decider = Uuid.CreateVersion4();
        var gap = new ReadinessGapView(Uuid.CreateVersion4(), "missing_input", "risks", "risk_reviewed", "Review risks.", []);
        await ProgramManagementServices.SeedAsync(provider, new ComplianceProgram(tenant, programId), program =>
        {
            Assert.Null(program.Create("SOC 2", new ProgramPlan(null, null, null, null, null, null), runner, "Runner", Now));
            return Result.Success;
        });
        await ReadinessSeed.AdoptAsync(reader, executor, content, tenant, programId, assessment,
            new ReadinessEvaluation([], [], [gap], new string('a', 64)), Now, runner, Now);
        await ReadinessSeed.PlanAsync(reader, executor, content, tenant, programId, gap.GapId, runner,
            new DateOnly(2027, 1, 1), "Close the gap.", runner, Now);
        var request = new DecideReadiness(Uuid.CreateVersion4(), tenant, programId, assessment, 2,
            ProgramReadiness.DoNotProceed, "Continue remediation.");
        var admitted = await bus.DispatchAsync(request, new RequestDispatchContext(ProgramManagementServices.Actor(decider),
            new HttpInvocation("PUT", "/synthetic/readiness", "/synthetic/readiness", "readiness-proof")), CancellationToken.None);
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);
        var record = await ReadinessProcesses.StartDecisionAsync(provider, tenant, programId, request.CommandId);
        var acquire = await ReadinessProcesses.StepAsync(provider, ReadinessEffects.DecisionProcessName, record,
            "acquire", nameof(AuthorizeReadinessGapPlanReadinessDecisionUse), Assert.Single(record.Targets));
        var acquired = await ReadinessProcesses.SendReceiverAsync(provider, acquire!);
        Assert.IsType<ReceiverOutcome.Applied>(acquired.Value);
        var program = await reader.HydrateAsync(new ProgramReadiness(tenant, programId, request.CommandId));
        var accepted = program.PendingDecision!;
        var terms = ProgramReadiness.DecisionTerms(accepted)!.Value;
        var failureProof = new ReadinessDecisionFailureProof(programId, assessment,
            "The admitted decision cannot proceed.", gap.GapId, NativeFactReference.Of(program.Stream, accepted),
            request.CommandId, terms.Generation, terms.Intent);
        failureProof = mismatch switch
        {
            "another_admission" => failureProof with { Admission = failureProof.Admission with { EventId = Uuid.CreateVersion4() } },
            "another_root" => failureProof with { OperationId = Uuid.CreateVersion4() },
            "another_generation" => failureProof with { Generation = terms.Generation + 1 },
            "another_intent" => failureProof with { Intent = terms.Intent with { ContentId = Uuid.CreateVersion4() } },
            "outside_gap" => failureProof with { GapId = Uuid.CreateVersion4() },
            _ => failureProof,
        };
        var proof = await content.WriteJsonAsync(tenant.ToString(), Uuid.CreateVersion4(),
            ReadinessContent.FailureSchema, failureProof);
        var command = new AbortReadinessDecision(Uuid.CreateVersion4(), request.CommandId, program.Stream,
            terms.Generation, 0, proof);
        var before = program.CommittedStreamPosition;
        if (mismatch == "changed_failure_replay")
        {
            var original = await ReadinessProcesses.SendReceiverAsync(provider, command);
            Assert.IsType<ReceiverOutcome.Applied>(original.Value);
            var changedProof = await content.WriteJsonAsync(tenant.ToString(), Uuid.CreateVersion4(),
                ReadinessContent.FailureSchema, failureProof with { Reason = "Different authoritative failure." });
            command = command with { FailureProof = changedProof };
            before = (await reader.HydrateAsync(new ProgramReadiness(tenant, programId))).CommittedStreamPosition;
        }

        // Act
        var refused = await ReadinessProcesses.SendReceiverAsync(provider, command);
        var after = await reader.HydrateAsync(new ProgramReadiness(tenant, programId, request.CommandId));
        var plan = await reader.HydrateAsync(new ReadinessGapPlan(tenant, programId, gap.GapId));

        // Assert
        if (mismatch == "valid")
        {
            Assert.IsType<ReceiverOutcome.Applied>(refused.Value);
            Assert.Null(after.PendingDecision);
            Assert.IsType<ReadinessDecisionCanceled>(after.TerminalDecision(request.CommandId));
            Assert.Equal(before + 1, after.CommittedStreamPosition);
        }
        else
        {
            Assert.Equal(RequestErrorKind.Conflict, refused.Error?.Kind);
            Assert.False(refused.Error!.IsTransient);
            Assert.Equal(before, after.CommittedStreamPosition);
            if (mismatch != "changed_failure_replay")
                Assert.Equal(accepted.Metadata.EventId, after.PendingDecision!.Metadata.EventId);
        }
        Assert.True(plan.Uses.HasActiveUse);
    }
    [Fact]
    public async Task ShouldReplayEqualWinningRunGivenSourcePreparationFailsAfterConcurrentAdmission()
    {
        // Arrange
        var sources = new WinningSources();
        await using var provider = ReadinessTestServices.Build(sources: sources);
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var bus = services.GetRequiredService<IRequestBus>();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var runner = Uuid.CreateVersion4();
        await ProgramManagementServices.SeedAsync(provider, new ComplianceProgram(tenant, program), owner =>
        {
            Assert.Null(owner.Create("SOC 2", new ProgramPlan(null, null, null, null, null, null), runner, "Runner", Now));
            return Result.Success;
        });
        var request = new RunReadinessAssessment(Uuid.CreateVersion4(), tenant, program, 0);
        var dispatch = new RequestDispatchContext(ProgramManagementServices.Actor(runner),
            new HttpInvocation("POST", "/synthetic/readiness", "/synthetic/readiness", "readiness-race"));
        OperationAcceptedResponse? winner = null;
        sources.CommitWinner = async () =>
        {
            await using var winningScope = provider.CreateAsyncScope();
            var committed = await winningScope.ServiceProvider.GetRequiredService<IRequestBus>()
                .DispatchAsync(request, dispatch, CancellationToken.None);
            Assert.True(committed.IsSuccess, committed.Error?.Message);
            winner = committed.Value;
        };

        // Act
        var recovered = await bus.DispatchAsync(request, dispatch, CancellationToken.None);
        var owner = await services.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new ProgramReadiness(tenant, program, request.AssessmentId));

        // Assert
        Assert.True(recovered.IsSuccess, recovered.Error?.Message);
        Assert.Equal(winner, recovered.Value);
        Assert.Equal(1UL, owner.Version);
        Assert.Equal(request.AssessmentId, owner.PendingAdoption!.Operation.RootOperationId);
    }

    [Fact]
    public async Task ShouldAdmitFreshCandidateGivenPlanChangedAfterPreAdmissionContentFailure()
    {
        // Arrange
        var store = new FailingAcknowledgmentContent(new FitzKvImmutableContentStore(new InMemoryKvClient()));
        await using var provider = ReadinessTestServices.Build(immutableContent: store);
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var executor = services.GetRequiredService<IAggregateExecutor>();
        var bus = services.GetRequiredService<IRequestBus>();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var runner = Uuid.CreateVersion4();
        var decider = Uuid.CreateVersion4();
        var assessment = Uuid.CreateVersion4();
        var gap = new ReadinessGapView(Uuid.CreateVersion4(), "missing_input", "risks", "risk_reviewed", "Review risks.", []);
        await ProgramManagementServices.SeedAsync(provider, new ComplianceProgram(tenant, program), owner =>
        {
            Assert.Null(owner.Create("SOC 2", new ProgramPlan(null, null, null, null, null, null), runner, "Runner", Now));
            return Result.Success;
        });
        await ReadinessSeed.AdoptAsync(reader, executor, store, tenant, program, assessment,
            new ReadinessEvaluation([], [], [gap], new string('a', 64)), Now, runner, Now);
        await ReadinessSeed.PlanAsync(reader, executor, store, tenant, program, gap.GapId, runner,
            new DateOnly(2027, 1, 1), "Original current plan.", runner, Now);
        var command = new DecideReadiness(Uuid.CreateVersion4(), tenant, program, assessment, 2,
            ProgramReadiness.DoNotProceed, "Continue remediation.");
        var dispatch = new RequestDispatchContext(ProgramManagementServices.Actor(decider),
            new HttpInvocation("PUT", "/synthetic/readiness", "/synthetic/readiness", "candidate-retry"));
        store.FailNextAcknowledgment = true;
        var failed = await bus.DispatchAsync(command, dispatch, CancellationToken.None);
        Assert.Equal(RequestErrorKind.Conflict, failed.Error?.Kind);
        Assert.True(failed.Error!.IsTransient);
        Assert.Null((await reader.HydrateAsync(new ProgramReadiness(tenant, program))).PendingDecision);
        await ReadinessSeed.PlanAsync(reader, executor, store, tenant, program, gap.GapId, runner,
            new DateOnly(2027, 1, 1), "New current plan after losing candidate.", runner, Now.AddMinutes(1));

        // Act
        var admitted = await bus.DispatchAsync(command, dispatch, CancellationToken.None);
        var owner = await reader.HydrateAsync(new ProgramReadiness(tenant, program, command.CommandId));
        var intent = await store.ReadJsonAsync<ReadinessDecisionIntent>(tenant.ToString(),
            ProgramReadiness.DecisionTerms(owner.PendingDecision)!.Value.Intent);
        var gaps = await ImmutableSelectionManifests.ReadAsync<ReadinessGapView>(store, tenant.ToString(), intent.GapSelection);

        // Assert
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);
        Assert.Equal(3, owner.Revision);
        Assert.Equal("New current plan after losing candidate.", Assert.Single(gaps).Plan!.Action);
        Assert.Equal(2, gaps[0].Plan!.Revision);
    }

    sealed class FailingAcknowledgmentContent(IImmutableContentStore inner) : IImmutableContentStore
    {
        public bool FailNextAcknowledgment { get; set; }

        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            if (FailNextAcknowledgment && schema == ReadinessContent.AcknowledgmentSchema)
            {
                FailNextAcknowledgment = false;
                return Task.FromException<ImmutableContentRef>(new ImmutableContentUnavailableException(
                    "The candidate acknowledgment was unavailable before its root admission."));
            }
            return inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        }

        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default) => inner.ReadVerifiedAsync(realm, reference, ct);
    }

    sealed class WinningSources : IReadinessSourceReader
    {
        public Func<Task>? CommitWinner { get; set; }

        public async ValueTask<Result<ReadinessSourceSet>> ReadAsync(Uuid tenantId, Uuid programId,
            DateTimeOffset asOf, CancellationToken ct = default)
        {
            var commit = CommitWinner;
            CommitWinner = null;
            if (commit is null)
                return Result<ReadinessSourceSet>.Success(ReadinessSourceSet.Empty);
            await commit();
            return Result<ReadinessSourceSet>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The losing candidate's source read is unavailable after the equal admission.", true));
        }
    }

}
