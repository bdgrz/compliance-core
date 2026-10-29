using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.PolicyDistribution.Rules;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Services;

/// <summary>Reads what an admitted participant result froze, from the verified intent and authored content.</summary>
static class CampaignAdmissions
{
    public static Task<ChildAdmissionIntent> ReadIntentAsync(IImmutableContentStore content, Uuid tenantId,
        PolicyParticipantResultAdmitted admitted, CancellationToken ct) =>
        PolicyCampaignContent.ReadAsync<ChildAdmissionIntent>(content, tenantId, admitted.Intent, ct);

    /// <summary>The complete normalized request the admission froze as the child's authored content.</summary>
    public static async Task<T> ReadAuthoredAsync<T>(IImmutableContentStore content, Uuid tenantId,
        PolicyParticipantResultAdmitted admitted, CancellationToken ct) where T : notnull
    {
        var intent = await ReadIntentAsync(content, tenantId, admitted, ct).ConfigureAwait(false);
        return await PolicyCampaignContent.ReadAsync<T>(content, tenantId, intent.CanonicalAuthoredContent, ct)
            .ConfigureAwait(false);
    }
}
