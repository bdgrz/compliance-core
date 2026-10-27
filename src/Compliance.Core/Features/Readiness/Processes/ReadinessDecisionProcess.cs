using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     The ordered readiness decision process: hold every frozen gap plan in canonical stream-key order, record the
///     decision once all are held, then settle every use as released. The root is the decision acceptance, so the
///     complete frozen intent exists on the program readiness before any gap plan is touched. A permanent proof
///     mismatch cancels the decision through <see cref="ReadinessDecisionFences" />, which also settles every use as
///     canceled. Recovery after that cancellation fences the decision again and settles every use before the process
///     becomes terminal. Exactly one of the two completing receivers applies, by decision kind.
/// </summary>
static class ReadinessDecisionProcess
{
    public const string Acquire = "acquire";
    public const string Complete = "complete";
    public const string Settle = "settle";

    public static ProcessDefinition Definition() => new(ReadinessEffects.DecisionProcessName,
        [nameof(ReadinessDecisionAccepted), nameof(TypeIEntryDecisionAccepted)], true,
        [
            new ProcessStageDefinition(Acquire, [nameof(AuthorizeReadinessGapPlanReadinessDecisionUse)]),
            new ProcessStageDefinition(Complete, [nameof(CompleteReadinessDecision), nameof(CompleteTypeIEntryDecision)],
                "One of the two, by decision kind."),
            new ProcessStageDefinition(Settle, [nameof(SettleReadinessGapPlanReadinessDecisionUse)]),
        ], [nameof(AbortReadinessDecision)], Receiver);

    /// <summary>The gap plan stream keys the process acquires and settles, in the frozen manifest's gap set.</summary>
    public static async ValueTask<IReadOnlyList<string>> TargetsAsync(IImmutableContentStore content, Uuid tenant,
        ReadinessDecisionIntent intent, CancellationToken ct)
    {
        var gaps = await ImmutableSelectionManifests.ReadAsync<ReadinessGapView>(content, tenant.ToString(),
            intent.GapSelection, ct).ConfigureAwait(false);
        return gaps.Select(gap => ReadinessEffects.GapKey(tenant, intent.ProgramId, gap.GapId)).ToArray();
    }

    static async ValueTask<IRequest<ReceiverOutcome>?> Receiver(ProcessStep step, IServiceProvider services,
        CancellationToken ct)
    {
        var record = step.Record;
        var tenant = Uuid.Parse(record.Key.Realm, System.Globalization.CultureInfo.InvariantCulture);
        var content = services.GetRequiredService<IImmutableContentStore>();
        var reader = services.GetRequiredService<IAggregateReader>();
        var verifier = services.GetRequiredService<ReadinessVerifier>();
        var root = record.Key.RootOperationId;
        var decision = await verifier.VerifyAcceptedAsync(record.Source, record.Intent, root, step.CommandId, ct)
            .ConfigureAwait(false);
        if (decision.Error is not null)
        {
            if (decision.Error.IsTransient)
                throw new SourceFactVerificationException(decision.Error.Message);
            throw new ImmutableContentIntegrityException(decision.Error.Message);
        }
        var intent = decision.Intent!;
        var generation = decision.Generation;
        var program = decision.Program!;
        switch (step.Receiver)
        {
            case nameof(AuthorizeReadinessGapPlanReadinessDecisionUse):
                {
                    var (gapId, plan, requirement) = await GapOfAsync(content, tenant, intent, step, ct).ConfigureAwait(false) ??
                        (Uuid.Empty, null, null);
                    return plan is null
                        ? null
                        : new AuthorizeReadinessGapPlanReadinessDecisionUse(step.CommandId, record.Source, plan.Stream,
                            root, generation, null, record.Intent, gapId, requirement!.ContentEvent.EventId,
                            requirement.ResourceRevision, intent.ProgramId);
                }
            case nameof(CompleteReadinessDecision) or nameof(CompleteTypeIEntryDecision):
                {
                    var kind = step.Receiver == nameof(CompleteReadinessDecision)
                        ? ReadinessDecisionKind.Readiness
                        : ReadinessDecisionKind.TypeIEntry;
                    if (intent.DecisionKind != kind)
                        return null;
                    ImmutableContentRef proofs;
                    if (program.TerminalDecision(root) is { } terminal)
                    {
                        // A terminal receiver decides before reading proofs; settled grants must never block replay.
                        proofs = await ReadinessContent.WriteDeterministicAsync(content, tenant,
                            Uuid.CreateVersion5(terminal.Metadata.EventId, "terminal-recovery-proofs"),
                            ReadinessContent.DecisionProofsSchema, (IReadOnlyList<ReadinessPlanProof>)[], ct)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        var held = await ReadinessDecisionProofs.HeldAsync(reader, content, decision, ct).ConfigureAwait(false);
                        if (held.Proofs is null && held.Transient)
                            throw new SourceFactVerificationException(held.Reason!);
                        // A permanent mismatch reaches the receiver, which cancels through its fence.
                        proofs = await ReadinessContent.WriteDeterministicAsync(content, tenant, step.CommandId,
                            ReadinessContent.DecisionProofsSchema, held.Proofs ?? [], ct).ConfigureAwait(false);
                    }
                    return kind == ReadinessDecisionKind.Readiness
                        ? new CompleteReadinessDecision(step.CommandId, record.Source, program.Stream, root, generation,
                            null, record.Intent, intent.ProgramId, intent.AssessmentId, proofs)
                        : new CompleteTypeIEntryDecision(step.CommandId, record.Source, program.Stream, root, generation,
                            null, record.Intent, intent.ProgramId, intent.AssessmentId, proofs);
                }
            case nameof(SettleReadinessGapPlanReadinessDecisionUse):
                {
                    // A decision canceled by a fence settles its uses as canceled; otherwise the recorded fact the
                    // completing receiver kept releases them after the irreversible decision.
                    var canceled = program.TerminalDecision(root) is ReadinessDecisionCanceled fence
                        ? NativeFactReference.Of(program.Stream, fence)
                        : null;
                    var terminal = canceled ?? record.Facts.GetValueOrDefault(nameof(CompleteReadinessDecision)) ??
                        record.Facts.GetValueOrDefault(nameof(CompleteTypeIEntryDecision))
                        ?? throw new SourceFactVerificationException(
                            "The recorded decision is not kept on the process yet.");
                    var (gapId, plan, _) = await GapOfAsync(content, tenant, intent, step, ct).ConfigureAwait(false) ??
                        (Uuid.Empty, null, null);
                    return plan is null
                        ? null
                        : new SettleReadinessGapPlanReadinessDecisionUse(step.CommandId, terminal, plan.Stream, root,
                            generation, null, record.Intent, gapId, intent.ProgramId, canceled is null
                                ? SourceUseSettlementDisposition.IrreversibleDecision
                                : SourceUseSettlementDisposition.Canceled);
                }
            case nameof(AbortReadinessDecision):
                {
                    var proof = program.TerminalDecision(root) is ReadinessDecisionCanceled canceled
                        ? canceled.SourceProofs ?? throw new SourceFactVerificationException(
                            "The readiness cancellation has no failure proof.")
                        : await ReadinessContent.WriteDeterministicAsync(content, tenant,
                            Uuid.CreateVersion5(record.Source.EventId, "process-abort-proof"), ReadinessContent.FailureSchema,
                            new ReadinessDecisionFailureProof(intent.ProgramId, intent.AssessmentId,
                                record.Note ?? "The readiness decision process was aborted.", null, record.Source, root, generation, record.Intent), ct)
                            .ConfigureAwait(false);
                    return new AbortReadinessDecision(step.CommandId, root, program.Stream, generation, 0, proof);
                }
            default:
                throw new InvalidOperationException($"{step.Receiver} is not a readiness decision receiver.");
        }
    }

    static async ValueTask<(Uuid GapId, ReadinessGapPlan? Plan, SourceUseRequirement? Requirement)?> GapOfAsync(
        IImmutableContentStore content, Uuid tenant, ReadinessDecisionIntent intent, ProcessStep step, CancellationToken ct)
    {
        if (step.Target is null)
            return null;
        IReadOnlyList<ReadinessGapView> gaps;
        IReadOnlyList<SourceUseRequirement> uses;
        try
        {
            gaps = await ImmutableSelectionManifests.ReadAsync<ReadinessGapView>(content, tenant.ToString(),
                intent.GapSelection, ct).ConfigureAwait(false);
            uses = await ImmutableSelectionManifests.ReadAsync<SourceUseRequirement>(content, tenant.ToString(),
                intent.RequiredPlanUses, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentUnavailableException exception)
        {
            throw new SourceFactVerificationException($"The frozen decision cannot be verified yet: {exception.Message}");
        }

        var gap = gaps.FirstOrDefault(item => ReadinessEffects.GapKey(tenant, intent.ProgramId, item.GapId) == step.Target)
            ?? throw new InvalidOperationException($"{step.Target} is not a gap of the frozen decision.");
        var plan = new ReadinessGapPlan(tenant, intent.ProgramId, gap.GapId);
        var requirement = uses.FirstOrDefault(use => use.SourceOwner == plan.Stream)
            ?? throw new InvalidOperationException($"{step.Target} is not a frozen plan use of the decision.");
        return (gap.GapId, plan, requirement);
    }
}
