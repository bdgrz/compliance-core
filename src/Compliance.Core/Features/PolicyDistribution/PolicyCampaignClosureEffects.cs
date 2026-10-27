using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>Deterministic identities and frozen terms of the campaign closure process.</summary>
static class PolicyCampaignClosureEffects
{
    public const string ProcessName = "PolicyCampaignClosureProcess";

    /// <summary>The command id the process driver derives for a receiver step of the root closure.</summary>
    public static Uuid ReceiverCommandId(Uuid rootSourceEventId, string receiver, string? target, string episode) =>
        EffectIdentity.CommandId(rootSourceEventId, $"{ProcessName}:{receiver}", target ?? "-", episode);

    /// <summary>The key of a participant's closure use: stable for every effect of the operation.</summary>
    public static ReservationKey UseKey(Uuid rootOperationId, EventStreamAddress firstOwner, long generation,
        EventStreamAddress participant) => new(rootOperationId, firstOwner, (ulong)generation, participant,
        SourceUseReservations.PurposeName(SourceUsePurpose.CampaignClosure));

    /// <summary>The process target of a participant: the resource of its stream, which is unique per person.</summary>
    public static string TargetOf(SourceUseRequirement requirement) => requirement.SourceOwner.Resource;
}
