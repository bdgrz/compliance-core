using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

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
