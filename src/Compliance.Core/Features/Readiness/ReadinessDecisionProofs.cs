using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>Derives the held gap plan proofs of a decision from the authoritative gap plan owners.</summary>
static class ReadinessDecisionProofs
{
    /// <summary>
    ///     The held proof of every frozen gap plan use in canonical key order, or why one is not held. A grant that is
    ///     not visible yet is transient. A plan that is no longer exactly the frozen one, or a use that was settled
    ///     before the decision completed, is a permanent mismatch naming the gap, so the caller fences the decision.
    /// </summary>
    public static async ValueTask<HeldPlans> HeldAsync(IAggregateReader reader, IImmutableContentStore content,
        ReadinessDecision decision, CancellationToken ct)
    {
        var (intent, tenant) = (decision.Intent!, decision.TenantId);
        IReadOnlyList<SourceUseRequirement> uses;
        IReadOnlyList<ReadinessGapView> gaps;
        try
        {
            uses = await ImmutableSelectionManifests.ReadAsync<SourceUseRequirement>(content, tenant.ToString(),
                intent.RequiredPlanUses, ct).ConfigureAwait(false);
            gaps = await ImmutableSelectionManifests.ReadAsync<ReadinessGapView>(content, tenant.ToString(),
                intent.GapSelection, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return HeldPlans.Failed($"The frozen decision manifests cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException, null);
        }

        var proofs = new List<ReadinessPlanProof>(gaps.Count);
        foreach (var gap in gaps.OrderBy(item => ReadinessEffects.GapKey(tenant, intent.ProgramId, item.GapId),
                     Kernel.StreamKeyOrder.Instance))
        {
            var plan = await reader.HydrateAsync(new ReadinessGapPlan(tenant, intent.ProgramId, gap.GapId).ForSourceUse(candidate => ReadinessEffects.UseKey(intent, decision.Generation, candidate.Stream)), ct)
                .ConfigureAwait(false);
            if (uses.FirstOrDefault(use => use.SourceOwner == plan.Stream) is not { } requirement)
                return HeldPlans.Failed("A gap is not a frozen plan use of this decision.", false, gap.GapId);
            var entry = plan.Uses.Find(ReadinessEffects.UseKey(intent, decision.Generation, plan.Stream));
            if (entry is not { GrantFact: { } grant, IsActive: true })
                return entry is { IsSettled: true }
                    ? HeldPlans.Failed("A gap plan hold was settled before the decision completed.", false, gap.GapId)
                    : HeldPlans.Failed("A gap plan is not held by this decision yet.", true, gap.GapId);
            var latest = plan.Latest?.Metadata!.EventId ?? Uuid.Empty;
            if (latest != requirement.ContentEvent.EventId || (ulong)plan.Revision != requirement.ResourceRevision)
                return HeldPlans.Failed("A gap plan is no longer exactly the plan the decision froze.", false, gap.GapId);
            proofs.Add(new ReadinessPlanProof(gap.GapId,
                plan.Latest is null ? null : NativeFactReference.Of(plan.Stream, plan.Latest),
                NativeFactReference.Of(plan.Stream, grant)));
        }

        return new HeldPlans(proofs, null, false, null);
    }

    /// <summary>The outcome of deriving held proofs.</summary>
    public sealed record HeldPlans(IReadOnlyList<ReadinessPlanProof>? Proofs, string? Reason, bool Transient, Uuid? GapId)
    {
        /// <summary>A gap plan that is not held or not exactly the frozen plan.</summary>
        public static HeldPlans Failed(string reason, bool transient, Uuid? gapId) => new(null, reason, transient, gapId);

        /// <summary>The request error a process step reports for a failure.</summary>
        public RequestError Error => new(RequestErrorKind.Conflict, Reason!, Transient);
    }
}
