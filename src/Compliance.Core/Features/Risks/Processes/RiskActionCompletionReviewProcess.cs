using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Policies.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Risks.Processes;

/// <summary>
///     The ordered risk action completion review process: hold every cited evidence request in canonical stream-key
///     order, record the review decision once all are held, then settle every use as released after the decision. The
///     root is the review acceptance, so the complete intent exists on the action before any evidence request is
///     touched; a request that is not visible yet is a wait, not a failure. A permanent proof mismatch cancels the
///     review through <see cref="RiskActionReviewFences" />, which also settles every use as canceled. Recovery after
///     that cancellation fences the review again and settles every use before the process becomes terminal.
/// </summary>
static class RiskActionCompletionReviewProcess
{
    public const string Acquire = "acquire";
    public const string Complete = "complete";
    public const string Settle = "settle";

    public static ProcessDefinition Definition() => new(RiskActionReviewEffects.ProcessName,
        [nameof(RiskTreatmentActionCompletionReviewAccepted)], true,
        [
            new ProcessStageDefinition(Acquire, [nameof(AuthorizeEvidenceRequestActionCompletionUse)]),
            new ProcessStageDefinition(Complete, [nameof(CompleteRiskTreatmentActionReview)]),
            new ProcessStageDefinition(Settle, [nameof(SettleEvidenceRequestActionCompletionUse)]),
        ], [nameof(AbortRiskTreatmentActionReview)], Receiver);

    static async ValueTask<IRequest<ReceiverOutcome>?> Receiver(ProcessStep step, IServiceProvider services,
        CancellationToken ct)
    {
        var record = step.Record;
        var tenant = Uuid.Parse(record.Key.Realm, System.Globalization.CultureInfo.InvariantCulture);
        var content = services.GetRequiredService<IImmutableContentStore>();
        var reader = services.GetRequiredService<IAggregateReader>();
        RiskActionCompletionReviewIntent intent;
        RiskActionReviewDecision decision;
        IReadOnlyList<ReviewedEvidenceRequest> cited;
        IReadOnlyList<SourceUseRequirement> uses;
        try
        {
            intent = await FindingContent.ReadAsync<RiskActionCompletionReviewIntent>(content, tenant, record.Intent, ct)
                .ConfigureAwait(false);
            decision = await FindingContent.ReadAsync<RiskActionReviewDecision>(content, tenant, intent.AuthoredReview,
                ct).ConfigureAwait(false);
            cited = await ImmutableSelectionManifests.ReadAsync<ReviewedEvidenceRequest>(content, record.Key.Realm,
                intent.CompletionEvidence, ct).ConfigureAwait(false);
            uses = await ImmutableSelectionManifests.ReadAsync<SourceUseRequirement>(content, record.Key.Realm,
                intent.RequiredSourceUses, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentUnavailableException exception)
        {
            throw new SourceFactUnavailableException($"The frozen review cannot be verified yet: {exception.Message}");
        }

        var root = record.Key.RootOperationId;
        var action = await reader.HydrateAsync(new RiskTreatmentAction(tenant, decision.ProgramId, decision.RiskId,
            intent.ActionId, selectedReviewRoot: root), ct).ConfigureAwait(false);
        if (action.AcceptedReview(root) is not { } accepted)
            throw new SourceFactUnavailableException("The accepted review is not visible yet.");
        var generation = accepted.Generation;
        switch (step.Receiver)
        {
            case nameof(AuthorizeEvidenceRequestActionCompletionUse):
                {
                    if (step.Target is null)
                        return null;
                    var item = EvidenceOf(step, tenant, decision, cited);
                    var evidence = new EvidenceRequest(tenant, decision.ProgramId, item.EvidenceRequestId);
                    var requirement = uses.First(use => use.SourceOwner == evidence.Stream);
                    return new AuthorizeEvidenceRequestActionCompletionUse(step.CommandId, record.Source,
                        evidence.Stream, root, generation, null, record.Intent, item.EvidenceRequestId,
                        item.Fulfilment.EventId, requirement.ResourceRevision, intent.ActionId, decision.ProgramId);
                }
            case nameof(CompleteRiskTreatmentActionReview):
                {
                    var review = new RiskActionReview(null, action, intent, decision, tenant);
                    ImmutableContentRef proofs;
                    if (action.TerminalReview(root) is { } terminal)
                    {
                        // A terminal receiver decides before reading proofs; settled grants must never block replay.
                        proofs = await FindingContent.WriteDeterministicAsync(content, tenant,
                            Uuid.CreateVersion5(terminal.Metadata.EventId, "terminal-recovery-proofs"),
                            RiskContent.ProofsSchema, (IReadOnlyList<EvidenceUseProof>)[], ct).ConfigureAwait(false);
                    }
                    else
                    {
                        var held = await RiskActionReviewProofs.HeldAsync(reader, content, review, generation,
                            step.CommandId, ct).ConfigureAwait(false);
                        if (!held.IsSuccess)
                        {
                            if (held.Error!.IsTransient)
                                throw new SourceFactUnavailableException(held.Error.Message);
                            throw new ImmutableContentIntegrityException(held.Error.Message);
                        }
                        proofs = await FindingContent.WriteDeterministicAsync(content, tenant, step.CommandId,
                            RiskContent.ProofsSchema, held.Value!, ct).ConfigureAwait(false);
                    }
                    return new CompleteRiskTreatmentActionReview(step.CommandId, record.Source, action.Stream, root,
                        generation, null, record.Intent, intent.ActionId, proofs);
                }
            case nameof(SettleEvidenceRequestActionCompletionUse):
                {
                    if (step.Target is null)
                        return null;
                    var terminal = action.TerminalReview(root);
                    var decided = terminal is null
                        ? record.Facts.GetValueOrDefault(nameof(CompleteRiskTreatmentActionReview))
                            ?? throw new SourceFactUnavailableException(
                                "The review decision is not recorded on the process yet.")
                        : NativeFactReference.Of(action.Stream, terminal);
                    var item = EvidenceOf(step, tenant, decision, cited);
                    return new SettleEvidenceRequestActionCompletionUse(step.CommandId, decided,
                        new EvidenceRequest(tenant, decision.ProgramId, item.EvidenceRequestId).Stream, root,
                        generation, null, record.Intent, item.EvidenceRequestId, intent.ActionId, decision.ProgramId,
                        terminal is RiskTreatmentActionCompletionReviewCanceled
                            ? SourceUseSettlementDisposition.Canceled
                            : SourceUseSettlementDisposition.IrreversibleDecision);
                }
            case nameof(AbortRiskTreatmentActionReview):
                {
                    var proof = action.TerminalReview(root) is RiskTreatmentActionCompletionReviewCanceled canceled
                        ? canceled.SourceProofs ?? throw new SourceFactVerificationException(
                            "The review cancellation has no failure proof.")
                        : await FindingContent.WriteDeterministicAsync(content, tenant,
                            Uuid.CreateVersion5(record.Source.EventId, "process-abort-proof"), RiskContent.FailureSchema,
                            new RiskActionReviewFailureProof(decision.ProgramId, decision.RiskId, intent.ActionId,
                                record.Note ?? "The review process was aborted.", null, record.Source, root, generation, record.Intent), ct).ConfigureAwait(false);
                    return new AbortRiskTreatmentActionReview(step.CommandId, root, action.Stream, generation, 0, proof);
                }
            default:
                throw new InvalidOperationException($"{step.Receiver} is not a completion review receiver.");
        }
    }

    static ReviewedEvidenceRequest EvidenceOf(ProcessStep step, Uuid tenant, RiskActionReviewDecision decision,
        IReadOnlyList<ReviewedEvidenceRequest> cited) =>
        cited.FirstOrDefault(item => RiskActionReviewEffects.EvidenceKey(tenant, decision.ProgramId,
            item.EvidenceRequestId) == step.Target)
        ?? throw new InvalidOperationException($"{step.Target} is not an evidence request of the frozen review.");
}
