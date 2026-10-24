using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

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
