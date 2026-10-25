using Bdgrz.Compliance.Features.SourceUse;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Seeds campaign state through the real owner paths without the request guards: a launch is the root native fact
///     of the campaign, a participant result goes through the production admission and then the production apply
///     receiver, so every seeded fact carries genuine provenance, content and committed metadata.
/// </summary>
static class CampaignSeed
{
    public static async Task<PolicyDistributionCampaign> LaunchAsync(IServiceProvider provider, Uuid tenantId,
        Uuid programId, Uuid campaignId, CampaignSubject subject, string audienceKind,
        IReadOnlyList<string> teams, Uuid rosterSnapshotId, string rosterSha, RosterAudience audience, DateOnly dueOn,
        string? instructions, ActorReference actor, Uuid actorMemberId, DateTimeOffset launchedAt)
    {
        await using var scope = provider.CreateAsyncScope();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var executor = scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        var entries = CampaignRules.Entries(audience, dueOn);
        var teamsRef = await PolicyCampaignContent.WriteAsync(content, tenantId, PolicyCampaignContent.TeamsSchema,
            teams, CancellationToken.None);
        var entriesRef = await PolicyCampaignContent.WriteAsync(content, tenantId, PolicyCampaignContent.EntriesSchema,
            entries, CancellationToken.None);
        var body = await PolicyCampaignContent.WriteAsync(content, tenantId, PolicyCampaignContent.RegistrationSchema,
            new CampaignRegistration(campaignId, entries.Count, 1), CancellationToken.None);
        var result = await PolicyCampaignContent.WriteResultAsync(content, tenantId,
            PolicyCampaignContent.RegistrationSchema, body, CancellationToken.None);
        var response = await FirstResponses.FreezeAsync(content, tenantId.ToString(),
            PolicyCampaignContent.OkResponse(PolicyCampaignContent.RegistrationSchema, body), CancellationToken.None);
        var stream = new PolicyDistributionCampaign(tenantId, campaignId).Stream;
        var provenance = PolicyCampaignContent.Provenance(campaignId, subject.Kind == "policy"
            ? "LaunchPolicyCampaign"
            : "LaunchTrainingCampaign", stream, stream, actor, null, new string('0', 64), launchedAt);
        var launched = await executor.ExecuteAsync(new PolicyDistributionCampaign(tenantId, campaignId),
            campaign => AggregateOutcome.Commit(campaign.LaunchCampaign(provenance, programId, subject, audienceKind,
                teamsRef, rosterSnapshotId, rosterSha, DateOnly.FromDateTime(launchedAt.UtcDateTime), dueOn,
                instructions, entries.Count, actor, actorMemberId, launchedAt, entriesRef, response, result)),
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(launched.IsSuccess, launched.Error?.Message);
        return await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new PolicyDistributionCampaign(tenantId, campaignId), CancellationToken.None);
    }

    /// <summary>Admits and applies an acknowledgement at the campaign's current revision.</summary>
    public static Task<Result<ReceiverOutcome>> AcknowledgeAsync(IServiceProvider provider, Uuid tenantId,
        Uuid programId, Uuid campaignId, Uuid personId, long policyVersion, string contentSha256,
        ActorReference recorder, ActorReference performer, bool onBehalf, DateTimeOffset at,
        Uuid? commandId = null, string text = PolicyDistributionCampaign.DefaultAcknowledgementText) =>
        ApplyAsync(provider, tenantId, programId, campaignId, personId, CampaignParticipantResultKind.PolicyAcknowledgement,
            commandId ?? Uuid.CreateVersion4(), at, (id, revision) => new AcknowledgePolicy(id, tenantId, programId,
                campaignId, personId, policyVersion, contentSha256, text, revision),
            nameof(AcknowledgePolicy), AdmittedChildKind.PolicyAcknowledgement,
            PolicyCampaignContent.AcknowledgeCommandSchema, recorder, performer, onBehalf);

    /// <summary>Admits and applies a completion at the campaign's current revision.</summary>
    public static Task<Result<ReceiverOutcome>> CompleteAsync(IServiceProvider provider, Uuid tenantId, Uuid programId,
        Uuid campaignId, Uuid personId, long requirementVersion, DateOnly completedOn, string source, string evidence,
        ActorReference recorder, DateTimeOffset at, Uuid? commandId = null) =>
        ApplyAsync(provider, tenantId, programId, campaignId, personId, CampaignParticipantResultKind.TrainingCompletion,
            commandId ?? Uuid.CreateVersion4(), at, (id, revision) => new RecordTrainingCompletion(id, tenantId,
                programId, campaignId, personId, requirementVersion, completedOn, source, evidence, revision),
            nameof(RecordTrainingCompletion), AdmittedChildKind.TrainingCompletion,
            PolicyCampaignContent.CompletionCommandSchema, recorder, null, false);

    /// <summary>Admits and applies an exception at the campaign's current revision.</summary>
    public static Task<Result<ReceiverOutcome>> ApproveWaiverAsync(IServiceProvider provider, Uuid tenantId,
        Uuid programId, Uuid campaignId, Uuid personId, string reason, DateOnly expiresOn, ActorReference approver,
        DateTimeOffset at, Uuid? commandId = null) =>
        ApplyAsync(provider, tenantId, programId, campaignId, personId, CampaignParticipantResultKind.Waiver,
            commandId ?? Uuid.CreateVersion4(), at, (id, revision) => new ApproveCampaignWaiver(id, tenantId, programId,
                campaignId, personId, reason, expiresOn, revision),
            nameof(ApproveCampaignWaiver), AdmittedChildKind.CampaignWaiver, PolicyCampaignContent.WaiverCommandSchema,
            approver, null, false);

    /// <summary>
    ///     Accepts and completes the closure of a campaign by its owner facts, freezing the exact admitted results and
    ///     participant requirements the production handler freezes. The participant uses are not driven; use the closure
    ///     process host for that.
    /// </summary>
    public static async Task CloseAsync(IServiceProvider provider, Uuid tenantId, Uuid campaignId, string rationale,
        ActorReference actor, DateTimeOffset at)
    {
        await using var scope = provider.CreateAsyncScope();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var executor = scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var campaigns = new CampaignSource(reader, content);
        var commandId = Uuid.CreateVersion4();
        var loaded = await campaigns.ReadAsync(tenantId, (await reader.HydrateAsync(
            new PolicyDistributionCampaign(tenantId, campaignId))).ProgramId, campaignId, CancellationToken.None,
            commandId);
        Assert.True(loaded.IsSuccess, loaded.Error?.Message);
        var state = loaded.Value;
        var campaign = state.Campaign;
        var stream = campaign.Stream;
        var request = new CloseCampaign(commandId, tenantId, campaign.ProgramId, campaignId, rationale,
            campaign.Revision);
        var provenance = PolicyCampaignContent.Provenance(commandId, "CloseCampaign", stream, stream, actor,
            campaign.Revision, new string('1', 64), at);
        var handler = new CloseCampaignHandler(new NativeOperationExecutor(scope.ServiceProvider.GetRequiredService<IAggregateReader>(), scope.ServiceProvider.GetRequiredService<IAggregateExecutor>(), content),
            campaigns, content, TimeProvider.System);
        var intent = await handler.FreezeAsync(state, request, provenance, at, CancellationToken.None);
        var accepted = await PolicyCampaignContent.AcceptedResponseAsync(content, tenantId, commandId, at,
            CancellationToken.None);
        var response = await FirstResponses.FreezeAsync(content, tenantId.ToString(), accepted, CancellationToken.None);
        var admitted = await executor.ExecuteAsync(new PolicyDistributionCampaign(tenantId, campaignId),
            owner => AggregateOutcome.Commit(owner.AcceptClosure(provenance, intent, response)),
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);

        var closedOn = DateOnly.FromDateTime(at.UtcDateTime);
        var totals = state.Totals(closedOn);
        var totalsRef = await PolicyCampaignContent.WriteAsync(content, tenantId, PolicyCampaignContent.TotalsSchema,
            totals, CancellationToken.None);
        var proofs = await PolicyCampaignContent.WriteAsync(content, tenantId,
            PolicyCampaignContent.ParticipantProofsSchema, (IReadOnlyList<CampaignParticipantProof>)[],
            CancellationToken.None);
        var view = state.ToView(closedOn) with { Status = "closed", ClosingTotals = totals };
        var body = await PolicyCampaignContent.WriteAsync(content, tenantId, PolicyCampaignContent.CampaignSchema, view,
            CancellationToken.None);
        var result = await PolicyCampaignContent.WriteResultAsync(content, tenantId,
            PolicyCampaignContent.CampaignSchema, body, CancellationToken.None);
        var header = PolicyCampaignContent.HeaderOf(provenance, stream, stream, at);
        var childProvenance = PolicyCampaignContent.Child(Uuid.CreateVersion4(), nameof(CompletePolicyCampaignClosure),
            Uuid.CreateVersion4(), header, stream, at);
        var closed = await executor.ExecuteAsync(new PolicyDistributionCampaign(tenantId, campaignId),
            owner => AggregateOutcome.Commit(owner.CompleteClosure(childProvenance, closedOn, rationale.Trim(),
                totalsRef, actor, at, proofs, intent, result)),
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(closed.IsSuccess, closed.Error?.Message);
    }

    /// <summary>Records a reconciliation of the audience at the campaign's current revision, by its owner fact.</summary>
    public static async Task<Result<CampaignReconciliationView>> ReconcileAsync(IServiceProvider provider,
        Uuid tenantId, Uuid programId, Uuid campaignId, Uuid rosterSnapshotId, string rosterSha, RosterAudience current,
        IReadOnlySet<Uuid> previouslyPresent, ActorReference actor, DateTimeOffset at)
    {
        await using var scope = provider.CreateAsyncScope();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var executor = scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var commandId = Uuid.CreateVersion4();
        var loaded = await new CampaignSource(reader, content).ReadAsync(tenantId, programId, campaignId,
            CancellationToken.None, commandId);
        Assert.True(loaded.IsSuccess, loaded.Error?.Message);
        var state = loaded.Value;
        var campaign = state.Campaign;
        var observedOn = DateOnly.FromDateTime(at.UtcDateTime);
        var list = CampaignRules.Amendments(state.Roster, current, previouslyPresent, campaign.Launch!.DueOn,
            observedOn);
        var view = new CampaignReconciliationView(campaignId, campaign.Reconciliations + 1, rosterSnapshotId, list);
        var amendments = await PolicyCampaignContent.WriteAsync(content, tenantId,
            PolicyCampaignContent.AmendmentsSchema, list, CancellationToken.None);
        var body = await PolicyCampaignContent.WriteAsync(content, tenantId,
            PolicyCampaignContent.ReconciliationSchema, view, CancellationToken.None);
        var result = await PolicyCampaignContent.WriteResultAsync(content, tenantId,
            PolicyCampaignContent.ReconciliationSchema, body, CancellationToken.None);
        var response = await FirstResponses.FreezeAsync(content, tenantId.ToString(),
            PolicyCampaignContent.OkResponse(PolicyCampaignContent.ReconciliationSchema, body), CancellationToken.None);
        var provenance = PolicyCampaignContent.Provenance(commandId, "ReconcileCampaignAudience", campaign.Stream,
            campaign.Stream, actor, campaign.Revision, new string('3', 64), at);
        var done = await executor.ExecuteAsync(new PolicyDistributionCampaign(tenantId, campaignId, commandId),
            owner => AggregateOutcome.Commit(owner.Reconcile(provenance, rosterSnapshotId, rosterSha, observedOn,
                amendments, actor, at, response, result)), new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
        return done.IsSuccess ? Result<CampaignReconciliationView>.Success(view)
            : Result<CampaignReconciliationView>.Failure(done.Error!);
    }

    /// <summary>Runs the apply receiver for a result the campaign admitted, as the process driver would.</summary>
    public static async Task<Result<ReceiverOutcome>> ApplyAdmittedAsync(IServiceProvider provider, Uuid tenantId,
        Uuid campaignId, Uuid personId, Uuid commandId, CampaignParticipantResultKind kind)
    {
        await using var scope = provider.CreateAsyncScope();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var executor = scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        var campaign = await reader.HydrateAsync(new PolicyDistributionCampaign(tenantId, campaignId));
        var fact = campaign.AdmittedFor(commandId)!;
        var command = new ApplyPolicyParticipantResult(Uuid.CreateVersion5(commandId, "apply"),
            NativeFactReference.Of(campaign.Stream, fact), PolicyCampaignParticipant.StreamOf(tenantId, campaignId,
                personId), commandId, fact.Generation, null, fact.Intent, campaignId, personId, kind);
        var handler = new ApplyPolicyParticipantResultHandler(reader, executor, content, TimeProvider.System);
        return await handler.HandleAsync(new Cntryl.Portia.RequestContext<ApplyPolicyParticipantResult>(command,
            new RequestDispatchContext(RequestActor.System)), CancellationToken.None);
    }

    /// <summary>The participant of a campaign and person as its owner says it is.</summary>
    public static async Task<PolicyCampaignParticipant> ParticipantAsync(IServiceProvider provider, Uuid tenantId,
        Uuid campaignId, Uuid personId, ReservationKey? selectedUse = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var owner = new PolicyCampaignParticipant(tenantId, campaignId, personId);
        if (selectedUse is not null)
            owner.ForSourceUses(selectedUse);
        return await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(owner);
    }

    static async Task<Result<ReceiverOutcome>> ApplyAsync<TRequest>(IServiceProvider provider, Uuid tenantId,
        Uuid programId, Uuid campaignId, Uuid personId, CampaignParticipantResultKind kind, Uuid commandId,
        DateTimeOffset at, Func<Uuid, long, TRequest> request, string commandKind, AdmittedChildKind childKind,
        string schema, ActorReference recorder, ActorReference? performer, bool onBehalf)
        where TRequest : IRequest<OperationAcceptedResponse>
    {
        await using var scope = provider.CreateAsyncScope();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var system = new RequestDispatchContext(RequestActor.System);
        var source = await new CampaignSource(reader, content).ReadAsync(tenantId, programId, campaignId,
            CancellationToken.None, commandId);
        Assert.True(source.IsSuccess, source.Error?.Message);
        var state = source.Value;
        var authored = request(commandId, state.Campaign.Revision);
        var plan = new ParticipantAdmissionPlan(commandId, personId, state.Campaign.Revision, commandKind, childKind,
            schema, recorder, performer, onBehalf, at);
        var admission = new PolicyParticipantAdmission(new NativeOperationExecutor(scope.ServiceProvider.GetRequiredService<IAggregateReader>(), scope.ServiceProvider.GetRequiredService<IAggregateExecutor>(), content),
            content);
        var admitted = await admission.AdmitAsync(new Cntryl.Portia.RequestContext<TRequest>(authored, system), state, plan,
            authored, _ => ValueTask.FromResult<CommandFailure?>(null), CancellationToken.None);
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);

        return await ApplyAdmittedAsync(provider, tenantId, campaignId, personId, commandId, kind);
    }
}
