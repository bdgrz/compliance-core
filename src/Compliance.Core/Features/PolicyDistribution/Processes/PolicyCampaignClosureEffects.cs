using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SourceUse.Services;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Processes;

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
