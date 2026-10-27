using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Features.PolicyDistribution.Processes;

/// <summary>
///     The ordered campaign closure process: acquire the exact applied results of every participant that has an
///     admitted result (the receiver waits until each admitted result is applied), complete the closure with the
///     frozen totals and the complete participant proofs, then settle every participant use. The root is the accepted
///     closure, so result admission is sealed and the complete intent exists on the campaign before any participant is
///     touched. The abort receiver cancels a closure that can never complete and fences its delayed finalization; the
///     settle stage then runs for acquired and not-yet-acquired uses alike.
/// </summary>
static class PolicyCampaignClosureProcess
{
    public const string Acquire = "acquire";
    public const string Complete = "complete";
    public const string Settle = "settle";

    public static ProcessDefinition Definition() => new(PolicyCampaignClosureEffects.ProcessName,
        [nameof(PolicyCampaignClosureAccepted)], true,
        [
            new ProcessStageDefinition(Acquire, [nameof(AuthorizePolicyCampaignParticipantCampaignClosureUse)],
                "Per participant in canonical stream-key order; waits for each admitted result to be applied."),
            new ProcessStageDefinition(Complete, [nameof(CompletePolicyCampaignClosure)]),
            new ProcessStageDefinition(Settle, [nameof(SettlePolicyCampaignParticipantCampaignClosureUse)],
                "Runs after an abort too, for held and not-yet-held uses."),
        ], [nameof(AbortPolicyCampaignClosure)], Receiver);

    static async ValueTask<IRequest<ReceiverOutcome>?> Receiver(ProcessStep step, IServiceProvider services,
        CancellationToken ct)
    {
        var record = step.Record;
        var tenantId = PolicyCampaignContent.ParseTenant(record.Key.Realm);
        var store = services.GetRequiredService<IImmutableContentStore>();
        ClosureIntent intent;
        IReadOnlyList<ClosureParticipant> participants;
        try
        {
            intent = await PolicyCampaignContent.ReadAsync<ClosureIntent>(store, tenantId, record.Intent, ct)
                .ConfigureAwait(false);
            participants = await ClosureParticipant.ReadAsync(store, tenantId, intent, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new SourceFactVerificationException(
                $"The frozen closure cannot be verified yet: {exception.Message}");
        }

        var root = record.Key.RootOperationId;
        var campaign = intent.Header.FirstOwner;
        var generation = (long)intent.ParentGeneration;
        ClosureParticipant? Participant() => step.Target is null
            ? null
            : participants.SingleOrDefault(candidate => candidate.Target == step.Target)
                ?? throw new InvalidOperationException($"{step.Target} is not held by the process record.");
        switch (step.Receiver)
        {
            case nameof(AuthorizePolicyCampaignParticipantCampaignClosureUse):
                {
                    if (Participant() is not { } held)
                        return null;
                    return new AuthorizePolicyCampaignParticipantCampaignClosureUse(step.CommandId, record.Source,
                        held.Requirement.SourceOwner, root, generation, null, record.Intent,
                        Uuid.Parse(held.Target, System.Globalization.CultureInfo.InvariantCulture),
                        held.Requirement.ContentEvent.EventId, checked((long)held.Requirement.ResourceRevision), intent.ParentId,
                        held.PersonId);
                }

            case nameof(CompletePolicyCampaignClosure):
                return new CompletePolicyCampaignClosure(step.CommandId, record.Source, campaign, root, generation,
                    null, record.Intent, intent.ParentId, intent.SourceUses.Content);
            case nameof(SettlePolicyCampaignParticipantCampaignClosureUse):
                {
                    if (Participant() is not { } held)
                        return null;
                    // The campaign's terminal fact proves the settlement: the closed campaign on the happy path, the
                    // cancellation after an abort. With neither recorded yet, nothing may be settled.
                    var closed = record.Facts.GetValueOrDefault(nameof(CompletePolicyCampaignClosure));
                    var canceled = record.Facts.GetValueOrDefault(nameof(AbortPolicyCampaignClosure));
                    var terminal = record.State == ProcessState.Aborting ? canceled ?? closed : closed ?? canceled;
                    if (terminal is null)
                        throw new SourceFactVerificationException(
                            "Neither the closed campaign nor the cancellation is recorded on the process yet.");
                    var disposition = terminal == closed
                        ? SourceUseSettlementDisposition.ParentClosed
                        : SourceUseSettlementDisposition.Canceled;
                    return new SettlePolicyCampaignParticipantCampaignClosureUse(step.CommandId, terminal,
                        held.Requirement.SourceOwner, root, generation, null, record.Intent,
                        Uuid.Parse(held.Target, System.Globalization.CultureInfo.InvariantCulture), intent.ParentId,
                        held.PersonId, disposition);
                }

            case nameof(AbortPolicyCampaignClosure):
                return new AbortPolicyCampaignClosure(step.CommandId, root, campaign, generation,
                    checked((long)intent.Header.OriginalExpectedRevision!.Value + 1), record.Intent);
            default:
                throw new InvalidOperationException($"{step.Receiver} is not a campaign closure receiver.");
        }
    }
}
