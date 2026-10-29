using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.PolicyDistribution.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.SourceUse.Models;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Models;

/// <summary>One participant a closure holds: the exact use requirement and the person whose results it pins.</summary>
sealed record ClosureParticipant(SourceUseRequirement Requirement, Uuid PersonId)
{
    /// <summary>The process target of the participant: the resource of its stream.</summary>
    public string Target => PolicyCampaignClosureEffects.TargetOf(Requirement);

    /// <summary>Reads the complete requirements and admitted results of a closure intent and joins them by participant.</summary>
    public static async Task<IReadOnlyList<ClosureParticipant>> ReadAsync(IImmutableContentStore content,
        Uuid tenantId, ClosureIntent intent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(intent);
        var requirements = await PolicyCampaignContent.ReadManifestAsync<SourceUseRequirement>(content, tenantId,
            intent.SourceUses, ct).ConfigureAwait(false);
        var admitted = await PolicyCampaignContent.ReadManifestAsync<CampaignAdmittedResult>(content, tenantId,
            intent.AdmittedChildren, ct).ConfigureAwait(false);
        var people = admitted.Select(result => result.PersonId).Distinct().ToDictionary(
            person => PolicyCampaignParticipant.StreamOf(tenantId, intent.ParentId, person), person => person);
        return requirements.Select(requirement => new ClosureParticipant(requirement,
            people.TryGetValue(requirement.SourceOwner, out var person)
                ? person
                : throw new ImmutableContentIntegrityException(
                    "A closure requirement names a participant without an admitted result."))).ToArray();
    }
}
