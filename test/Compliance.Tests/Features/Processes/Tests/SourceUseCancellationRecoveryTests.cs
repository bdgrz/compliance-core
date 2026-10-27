using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Policies.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Programs.Rules;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Readiness.Services;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.History;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Processes;
using Bdgrz.Compliance.Features.Risks.Rules;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
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
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
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
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Readiness.Aggregates;
using Bdgrz.Compliance.Features.Readiness.Commands;
using Bdgrz.Compliance.Features.Readiness.Events;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Processes.Tests;

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
        await ProgramManagementServices.SeedAsync(provider, new ProgramRules(tenant, programId), owner =>
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
            new ReadinessDecisionFailureProof(programId, assessmentId, "The decision can no longer proceed.", null,
                record.Source, request.CommandId, generation, record.Intent));
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
        var reference = new Bdgrz.Compliance.Features.Proofs.Models.ImmutableContentRef(Uuid.CreateVersion4(),
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
        public Task<Bdgrz.Compliance.Features.Proofs.Models.ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId,
            string schema, string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm,
            Bdgrz.Compliance.Features.Proofs.Models.ImmutableContentRef reference, CancellationToken ct = default) =>
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
