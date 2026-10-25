using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Readiness;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.SourceUse;
using Bdgrz.Compliance.Tests.Features.Risks;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Processes;

public sealed class SourceUseCancellationRecoveryTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public async Task ShouldSettleEvidenceUsesGivenCancellationPersistedBeforeProcessCheckpoint(int stageIndex,
        bool partiallySettled)
    {
        // Arrange
        var fixture = await RiskGovernanceHandlerTests.Fixture.CreateAsync("mitigate");
        await using var provider = fixture.Provider;
        var worker = await fixture.SeedMemberAsync();
        var evidenceIds = new[] { await fixture.SeedEvidenceAsync(true, worker), await fixture.SeedEvidenceAsync(true, worker) };
        var added = await fixture.Scenario(fixture.AssessorUserId).When(fixture.AddAction(worker, evidenceIds, 0))
            .ExpectSuccess();
        var submitted = await PersonalRiskSignoffTransportTests.HttpAsync(provider, fixture.AssessorUserId,
            fixture.Submit(added.Value.ActionId, 1, evidenceIds));
        await fixture.AssignReviewAsync(submitted.Value.SubmissionId);
        var request = fixture.ReviewAction(added.Value.ActionId, 2, "accept");
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var acceptedResult = await services.GetRequiredService<IRequestBus>().DispatchAsync(request,
            Personal(fixture.ApproverUserId), CancellationToken.None);
        Assert.True(acceptedResult.IsSuccess, acceptedResult.Error?.Message);
        var reader = services.GetRequiredService<IAggregateReader>();
        var content = services.GetRequiredService<IImmutableContentStore>();
        var action = await reader.HydrateAsync(new RiskTreatmentAction(fixture.TenantId, fixture.ProgramId,
            fixture.RiskId, request.ActionId, selectedReviewRoot: request.CommandId));
        var accepted = action.AcceptedReview(request.CommandId)!;
        var source = NativeFactReference.Of(action.Stream, accepted);
        var key = ProcessKey.ForRoot(RiskActionReviewEffects.ProcessName, request.CommandId, source);
        var records = services.GetRequiredService<IProcessRecordStore>();
        var record = await records.CreateIfAbsentAsync(ProcessRecord.Start(key, source, accepted.Operation.Episode,
            accepted.Intent, evidenceIds.Select(id => RiskActionReviewEffects.EvidenceKey(fixture.TenantId,
                fixture.ProgramId, id)), DateTimeOffset.UtcNow));
        foreach (var target in record.Targets)
            Assert.IsType<ReceiverOutcome.Applied>((await SendAsync(provider, (await StepAsync(provider, record,
                "acquire", nameof(AuthorizeEvidenceRequestActionCompletionUse), target))!)).Value);
        var delayedComplete = (await StepAsync(provider, record, "complete", nameof(CompleteRiskTreatmentActionReview),
            null))!;
        record = await records.ReplaceAsync(record with { StageIndex = stageIndex }, record.Version);
        var proof = await content.WriteJsonAsync(fixture.TenantId.ToString(), Uuid.CreateVersion4(), RiskContent.FailureSchema,
            new RiskActionReviewFailureProof(fixture.ProgramId, fixture.RiskId, request.ActionId,
                "The completion can no longer be accepted.", null, source, request.CommandId, accepted.Generation, accepted.Intent));
        var abort = new AbortRiskTreatmentActionReview(CommandId(record, nameof(AbortRiskTreatmentActionReview), null),
            request.CommandId, action.Stream, accepted.Generation, 0, proof);
        var aborted = Assert.IsType<ReceiverOutcome.Applied>((await SendAsync(provider, abort)).Value);
        if (partiallySettled)
        {
            var first = evidenceIds.Single(id => RiskActionReviewEffects.EvidenceKey(fixture.TenantId,
                fixture.ProgramId, id) == record.Targets[0]);
            await SendAsync(provider, new SettleEvidenceRequestActionCompletionUse(
                CommandId(record, nameof(SettleEvidenceRequestActionCompletionUse), record.Targets[0]), aborted.Fact,
                new EvidenceRequest(fixture.TenantId, fixture.ProgramId, first).Stream, request.CommandId,
                accepted.Generation, null, record.Intent, first, request.ActionId, fixture.ProgramId,
                SourceUseSettlementDisposition.Canceled));
        }

        // Act: resume after the domain cancellation but before the process recorded it or released every use.
        var recovered = await services.GetRequiredService<ProcessDriver>().RunAsync(key, "recovery-worker",
            new RequestDispatchContext(RequestActor.System));
        var delayed = await SendAsync(provider, delayedComplete);

        // Assert
        Assert.Equal(ProcessState.Aborted, recovered.State);
        foreach (var id in evidenceIds)
            Assert.False((await reader.HydrateAsync(new EvidenceRequest(fixture.TenantId, fixture.ProgramId, id)))
                .Uses.HasActiveUse);
        var retained = await reader.HydrateAsync(new RiskTreatmentAction(fixture.TenantId, fixture.ProgramId,
            fixture.RiskId, request.ActionId, selectedReviewRoot: request.CommandId));
        Assert.Null(retained.PendingReview);
        Assert.IsType<RiskTreatmentActionCompletionReviewCanceled>(retained.TerminalReview(request.CommandId));
        Assert.Empty((await RiskTreatmentActionHistory.ReadAsync(
            services.GetRequiredService<IDomainEventReader>(), retained)).OfType<RiskTreatmentActionCompletionReviewed>());
        Assert.IsType<ReceiverOutcome.Rejected>(delayed.Value);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public async Task ShouldSettleReadinessPlanUsesGivenCancellationPersistedBeforeProcessCheckpoint(int stageIndex,
        bool partiallySettled)
    {
        // Arrange
        await using var provider = ReadinessTestServices.Build();
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var executor = services.GetRequiredService<IAggregateExecutor>();
        var content = services.GetRequiredService<IImmutableContentStore>();
        var tenant = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var assessmentId = Uuid.CreateVersion4();
        var runner = Uuid.CreateVersion4();
        var deciderUser = Uuid.CreateVersion4();
        var gaps = new[]
        {
            new ReadinessGapView(Uuid.CreateVersion4(), "missing_input", "risks", "risk_reviewed", "Review risks.", []),
            new ReadinessGapView(Uuid.CreateVersion4(), "missing_input", "evidence", "evidence_reviewed", "Collect evidence.", []),
        };
        var now = DateTimeOffset.UtcNow;
        await ProgramManagementServices.SeedAsync(provider, new ComplianceProgram(tenant, programId), owner =>
        {
            Assert.Null(owner.Create("SOC 2", new ProgramPlan(null, null, null, null, null, null), runner, "Runner", now));
            return Result.Success;
        });
        await ReadinessSeed.AdoptAsync(reader, executor, content, tenant, programId, assessmentId,
            new ReadinessEvaluation([], [], gaps, new string('a', 64)), now, runner, now);
        foreach (var gap in gaps)
            await ReadinessSeed.PlanAsync(reader, executor, content, tenant, programId, gap.GapId, runner,
                DateOnly.FromDateTime(now.UtcDateTime).AddDays(30), "Close the gap.", runner, now);
        var request = new DecideReadiness(Uuid.CreateVersion4(), tenant, programId, assessmentId, 2,
            ProgramReadiness.DoNotProceed, "Continue remediation.");
        var accepted = await services.GetRequiredService<IRequestBus>().DispatchAsync(request, Personal(deciderUser),
            CancellationToken.None);
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        var record = await ReadinessProcesses.StartDecisionAsync(provider, tenant, programId, request.CommandId);
        foreach (var target in record.Targets)
            Assert.IsType<ReceiverOutcome.Applied>((await SendAsync(provider, (await StepAsync(provider, record,
                "acquire", nameof(AuthorizeReadinessGapPlanReadinessDecisionUse), target))!)).Value);
        var delayedComplete = (await StepAsync(provider, record, "complete", nameof(CompleteReadinessDecision), null))!;
        var records = services.GetRequiredService<IProcessRecordStore>();
        record = await records.ReplaceAsync(record with { StageIndex = stageIndex }, record.Version);
        var program = await reader.HydrateAsync(new ProgramReadiness(tenant, programId));
        var generation = ProgramReadiness.DecisionTerms(program.AcceptedDecision(request.CommandId))!.Value.Generation;
        var proof = await content.WriteJsonAsync(tenant.ToString(), Uuid.CreateVersion4(), ReadinessContent.FailureSchema,
            new ReadinessDecisionFailureProof(programId, assessmentId, "The decision can no longer proceed.", null));
        var aborted = Assert.IsType<ReceiverOutcome.Applied>((await SendAsync(provider, new AbortReadinessDecision(
            CommandId(record, nameof(AbortReadinessDecision), null), request.CommandId, program.Stream, generation, 0,
            proof))).Value);
        if (partiallySettled)
        {
            var first = gaps.Single(gap => ReadinessEffects.GapKey(tenant, programId, gap.GapId) == record.Targets[0]);
            await SendAsync(provider, new SettleReadinessGapPlanReadinessDecisionUse(
                CommandId(record, nameof(SettleReadinessGapPlanReadinessDecisionUse), record.Targets[0]), aborted.Fact,
                new ReadinessGapPlan(tenant, programId, first.GapId).Stream, request.CommandId, generation, null,
                record.Intent, first.GapId, programId, SourceUseSettlementDisposition.Canceled));
        }

        // Act: resume after the domain cancellation but before the process recorded it or released every use.
        var recovered = await services.GetRequiredService<ProcessDriver>().RunAsync(record.Key, "recovery-worker",
            new RequestDispatchContext(RequestActor.System));
        var delayed = await SendAsync(provider, delayedComplete);

        // Assert
        Assert.Equal(ProcessState.Aborted, recovered.State);
        foreach (var gap in gaps)
            Assert.False((await reader.HydrateAsync(new ReadinessGapPlan(tenant, programId, gap.GapId))).Uses.HasActiveUse);
        var retained = await reader.HydrateAsync(new ProgramReadiness(tenant, programId));
        Assert.Null(retained.PendingDecision);
        Assert.Null(retained.DecisionOf(assessmentId));
        Assert.IsType<ReadinessDecisionCanceled>(retained.TerminalDecision(request.CommandId));
        Assert.IsType<ReceiverOutcome.Rejected>(delayed.Value);
    }

    [Theory]
    [InlineData("risk", true)]
    [InlineData("risk", false)]
    [InlineData("readiness-run", true)]
    [InlineData("readiness-run", false)]
    [InlineData("readiness-decision", true)]
    [InlineData("readiness-decision", false)]
    public async Task ShouldKeepUnavailableContentPendingAndCorruptContentPermanentGivenReceiverVerification(
        string kind, bool unavailable)
    {
        // Arrange
        await using var provider = ReadinessTestServices.Build();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var source = new NativeFactReference(new ProgramReadiness(tenant, program).Stream, Uuid.CreateVersion4(), 1);
        var reference = new Bdgrz.Compliance.Features.Proofs.ImmutableContentRef(Uuid.CreateVersion4(),
            new string('a', 64), 0, "application/json", "test-content.v1");
        var content = new UnreadableContent(unavailable);

        // Act
        var error = kind switch
        {
            "risk" => (await new RiskActionReviewVerifier(reader, content).VerifyAcceptedAsync(source, reference,
                operation, Uuid.CreateVersion4(), CancellationToken.None)).Error,
            "readiness-run" => (await new ReadinessVerifier(reader, content).VerifyRunAsync(source, reference,
                operation, Uuid.CreateVersion4(), CancellationToken.None)).Error,
            _ => (await new ReadinessVerifier(reader, content).VerifyAcceptedAsync(source, reference,
                operation, Uuid.CreateVersion4(), CancellationToken.None)).Error,
        };

        // Assert
        Assert.NotNull(error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.Equal(unavailable, error.IsTransient);
    }

    sealed class UnreadableContent(bool unavailable) : IImmutableContentStore
    {
        public Task<Bdgrz.Compliance.Features.Proofs.ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId,
            string schema, string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm,
            Bdgrz.Compliance.Features.Proofs.ImmutableContentRef reference, CancellationToken ct = default) =>
            Task.FromException<ReadOnlyMemory<byte>>(unavailable
                ? new ImmutableContentUnavailableException("Not readable yet.")
                : new ImmutableContentIntegrityException("The stored digest is invalid."));
    }

    static Uuid CommandId(ProcessRecord record, string receiver, string? target) => EffectIdentity.CommandId(
        record.Source.EventId, $"{record.Key.ProcessKind}:{receiver}", target ?? "-", record.Episode);

    static async Task<IRequest<ReceiverOutcome>?> StepAsync(IServiceProvider provider, ProcessRecord record,
        string stage, string receiver, string? target)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        return await services.GetRequiredService<ProcessCatalog>().Get(record.Key.ProcessKind).CreateReceiver(
            new ProcessStep(record, stage, receiver, target, CommandId(record, receiver, target)), services,
            CancellationToken.None);
    }

    static async Task<Result<ReceiverOutcome>> SendAsync(IServiceProvider provider, IRequest<ReceiverOutcome> request)
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result;
    }

    static RequestDispatchContext Personal(Uuid user) => new(ProgramManagementServices.Actor(user),
        new HttpInvocation("POST", "/synthetic/review", "/synthetic/review", "cancellation-recovery"));
}
