using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Arranges actual split-owner histories for consumer tests without unrelated command admission prerequisites.</summary>
static class RiskSeeds
{
    public static OperationProvenance Operation<T>(Aggregate owner, Uuid id, ulong? expected, ActorReference actor,
        DateTimeOffset at, string kind, T authored) where T : notnull =>
        new(id, id, null, kind, actor, owner.Stream, "seed", expected,
            ContentDigest.Sha256(System.Text.Encoding.UTF8.GetBytes($"{kind}|{id}|{owner.Stream}|{expected}|{actor.Id}|{at:O}|{authored}")),
            at, owner.Stream, owner.Stream);

    public static async Task AssignOwnerAsync(IServiceProvider provider, Uuid tenant, Uuid program, Uuid risk,
        Uuid person, Uuid? member, string rationale, ActorReference actor, DateTimeOffset at)
    {
        await using var scope = provider.CreateAsyncScope();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var owner = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new RiskGovernance(tenant, program, risk));
        var view = new RiskOwnerView(person, member, rationale, actor, at);
        var body = await FindingContent.WriteAsync(content, tenant, RiskContent.OwnerSchema, view, CancellationToken.None);
        var (first, result) = await UnitAsync(content, tenant);
        var operation = Operation(owner, Uuid.CreateVersion4(), (ulong)owner.Revision, actor, at, nameof(AssignRiskOwner), view);
        await ProgramManagementServices.SeedAsync(provider, new RiskGovernance(tenant, program, risk),
            source => source.AssignOwner(operation, body, first, result));
    }

    public static async Task ProposeTreatmentAsync(IServiceProvider provider, Uuid tenant, Uuid program, Uuid risk,
        Uuid treatment, Uuid control, Uuid version, string rationale, Uuid proposer, ActorReference actor, DateTimeOffset at)
    {
        await using var scope = provider.CreateAsyncScope();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var owner = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new RiskTreatmentAssertion(tenant, program, risk, control));
        var view = new RiskControlTreatmentView(treatment, risk, control, version, RiskStates.TreatmentProposed,
            rationale, actor, at, null, null, null, null, null, null, null, null)
        { Revision = owner.Revision + 1 };
        var body = await FindingContent.WriteAsync(content, tenant, RiskContent.TreatmentSchema, view, CancellationToken.None);
        var (response, result) = await FindingContent.FreezeAsync(content, tenant,
            RiskContent.ControlTreatmentRegistrationSchema, new RiskControlTreatmentRegistration(treatment, owner.Revision + 1),
            CancellationToken.None);
        var first = await FirstResponses.FreezeAsync(content, tenant.ToString(), response);
        var operation = Operation(owner, treatment, (ulong)owner.Revision, actor, at, nameof(ProposeRiskControlTreatment), view);
        await ProgramManagementServices.SeedAsync(provider, new RiskTreatmentAssertion(tenant, program, risk, control),
            source => source.Propose(operation, body, proposer, first, result));
    }

    public static async Task ReviewTreatmentAsync(IServiceProvider provider, Uuid tenant, Uuid program, Uuid risk,
        Uuid control, Uuid treatment, string outcome, string rationale, ActorReference actor, DateTimeOffset at)
    {
        await using var scope = provider.CreateAsyncScope();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var owner = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new RiskTreatmentAssertion(tenant, program, risk, control));
        var (first, result) = await UnitAsync(content, tenant);
        var operation = Operation(owner, Uuid.CreateVersion4(), (ulong)owner.Revision, actor, at,
            nameof(ReviewRiskControlTreatment), new { treatment, outcome, rationale });
        await ProgramManagementServices.SeedAsync(provider, new RiskTreatmentAssertion(tenant, program, risk, control),
            source => source.Review(operation, treatment, outcome, rationale, actor, at, null, first, result));
    }

    public static async Task ReviewTreatmentAsync(IServiceProvider provider, Uuid tenant, Uuid program, Uuid risk,
        Uuid treatment, string outcome, string rationale, ActorReference actor, DateTimeOffset at)
    {
        await using var scope = provider.CreateAsyncScope();
        var control = await scope.ServiceProvider.GetRequiredService<RiskGovernanceReader>()
            .LocateControlAsync(tenant, program, risk, treatment, CancellationToken.None)
            ?? throw new InvalidOperationException("The seeded treatment was not found.");
        await ReviewTreatmentAsync(provider, tenant, program, risk, control, treatment, outcome, rationale, actor, at);
    }

    public static async Task AddActionAsync(IServiceProvider provider, Uuid tenant, Uuid program, Uuid risk, Uuid action,
        string title, string targetState, string expectedEvidence, DateOnly due, Uuid accountable,
        IReadOnlyList<Uuid> evidence, ActorReference actor, DateTimeOffset at)
    {
        await using var scope = provider.CreateAsyncScope();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var owner = new RiskTreatmentAction(tenant, program, risk, action);
        var view = new RiskTreatmentActionView(action, risk, title, targetState, expectedEvidence, due, accountable,
            evidence, RiskStates.ActionOpen, false, actor, at, [])
        { Revision = 1 };
        var body = await FindingContent.WriteAsync(content, tenant, RiskContent.ActionSchema, view, CancellationToken.None);
        var (response, result) = await FindingContent.FreezeAsync(content, tenant, RiskContent.ActionRegistrationSchema,
            new RiskTreatmentActionRegistration(action, 1), CancellationToken.None);
        var first = await FirstResponses.FreezeAsync(content, tenant.ToString(), response);
        var operation = Operation(owner, action, 0, actor, at, nameof(AddRiskTreatmentAction), view);
        await ProgramManagementServices.SeedAsync(provider, owner, source => source.Add(operation, body, first, result));
    }

    public static async Task SubmitActionAsync(IServiceProvider provider, Uuid tenant, Uuid program, Uuid risk,
        Uuid action, Uuid submission, string summary, IReadOnlyList<Uuid> evidence, Uuid submitter,
        ActorReference actor, DateTimeOffset at)
    {
        await using var scope = provider.CreateAsyncScope();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var owner = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new RiskTreatmentAction(tenant, program, risk, action));
        var body = await FindingContent.WriteAsync(content, tenant, RiskContent.SubmittedEvidenceSchema, evidence, CancellationToken.None);
        var (response, result) = await FindingContent.FreezeAsync(content, tenant, RiskContent.CompletionRegistrationSchema,
            new RiskTreatmentActionCompletionRegistration(submission, owner.Revision + 1), CancellationToken.None);
        var first = await FirstResponses.FreezeAsync(content, tenant.ToString(), response);
        var operation = Operation(owner, submission, (ulong)owner.Revision, actor, at,
            nameof(SubmitRiskTreatmentActionCompletion), new { action, summary, evidence, submitter });
        await ProgramManagementServices.SeedAsync(provider, new RiskTreatmentAction(tenant, program, risk, action),
            source => source.Submit(operation, summary, body, submitter, actor, at, first, result));
    }

    public static async Task ReviewActionAsync(IServiceProvider provider, Uuid tenant, Uuid program, Uuid risk,
        Uuid action, string outcome, string rationale, ActorReference actor, DateTimeOffset at)
    {
        await using var scope = provider.CreateAsyncScope();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var owner = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new RiskTreatmentAction(tenant, program, risk, action));
        var id = Uuid.CreateVersion4();
        var operation = Operation(owner, id, (ulong)owner.Revision, actor, at, nameof(ReviewRiskTreatmentActionCompletion),
            new { outcome, rationale });
        var decision = await FindingContent.WriteAsync(content, tenant, RiskContent.AuthoredReviewSchema,
            new RiskActionReviewDecision(id, program, risk, outcome, rationale, Uuid.Parse(actor.Id), actor, at, null, null),
            CancellationToken.None);
        var cited = await ImmutableSelectionManifests.WriteAsync<ReviewedEvidenceRequest>(content, tenant.ToString(),
            Uuid.CreateVersion4(), "risk-action-reviewed-evidence.v1", [], SelectionOrder.Authored, CancellationToken.None);
        var uses = await ImmutableSelectionManifests.WriteAsync<SourceUseRequirement>(content, tenant.ToString(),
            Uuid.CreateVersion4(), "source-use-requirement.v1", [], SelectionOrder.Authored, CancellationToken.None);
        var header = new DurableIntentHeader(id, owner.Stream, owner.Stream, owner.Stream, actor, operation.Episode,
            operation.OriginalExpectedRevision, operation.AuthoredIntentSha256, at);
        var intent = await FindingContent.WriteAsync(content, tenant, RiskActionCompletionReviewIntent.Schema,
            new RiskActionCompletionReviewIntent(header, action, owner.PendingSubmission!.SubmissionId, decision, cited, uses),
            CancellationToken.None);
        var (_, result) = await UnitAsync(content, tenant);
        var acknowledgment = await FindingContent.AcceptedResponseAsync(content, tenant, id,
            RegisterFirmStaffHandler.StatusUrlTemplate, at, CancellationToken.None);
        var first = await FirstResponses.FreezeAsync(content, tenant.ToString(), acknowledgment);
        await ProgramManagementServices.SeedAsync(provider, new RiskTreatmentAction(tenant, program, risk, action),
            source => source.AcceptReview(operation, intent, first));
        var acceptedOwner = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new RiskTreatmentAction(tenant, program, risk, action));
        var child = operation with
        {
            LogicalId = Uuid.CreateVersion4(),
            CausationEventId = acceptedOwner.AcceptedReview(id)!.Metadata.EventId,
            CommandKind = nameof(CompleteRiskTreatmentActionReview),
        };
        await ProgramManagementServices.SeedAsync(provider, new RiskTreatmentAction(tenant, program, risk, action),
            source => source.CompleteReview(child, outcome, rationale, actor, at, null, intent, result));
    }

    public static async Task CancelActionAsync(IServiceProvider provider, Uuid tenant, Uuid program, Uuid risk,
        Uuid action, string rationale, ActorReference actor, DateTimeOffset at)
    {
        await using var scope = provider.CreateAsyncScope();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var owner = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new RiskTreatmentAction(tenant, program, risk, action));
        var (first, result) = await UnitAsync(content, tenant);
        var operation = Operation(owner, Uuid.CreateVersion4(), (ulong)owner.Revision, actor, at,
            nameof(CancelRiskTreatmentAction), rationale);
        await ProgramManagementServices.SeedAsync(provider, new RiskTreatmentAction(tenant, program, risk, action),
            source => source.Cancel(operation, rationale, actor, at, first, result));
    }

    static async Task<(ImmutableContentRef First, ImmutableContentRef Result)> UnitAsync(IImmutableContentStore content, Uuid tenant)
    {
        var (response, result) = await RiskCommands.FreezeUnitAsync(content, tenant, CancellationToken.None);
        return (await FirstResponses.FreezeAsync(content, tenant.ToString(), response), result);
    }
}
