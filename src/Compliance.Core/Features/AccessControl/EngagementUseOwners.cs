using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Resolves the source owner that holds one required source use of an engagement operation.</summary>
static class EngagementUseOwners
{
    /// <summary>
    ///     Hydrates the owner of <paramref name="stream" />, or returns null when the area is not a source use owner.
    ///     The duty designation is scoped by the original actor of the frozen header, who is the partner.
    /// </summary>
    public static async ValueTask<ISourceUseOwner?> HydrateAsync(IAggregateReader reader, EventStreamAddress stream,
        DurableIntentHeader header, Uuid tenantId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(header);
        switch (stream.Area)
        {
            case ClientServiceIndependence.Area:
                return await reader.HydrateAsync(new ClientServiceIndependence(tenantId), ct).ConfigureAwait(false);
            case FirmStaffRecord.Area when Uuid.TryParse(stream.Resource, out var staffId):
                return await reader.HydrateAsync(new FirmStaffRecord(staffId), ct).ConfigureAwait(false);
            case FirmProfessionalDutyDesignation.Area when Uuid.TryParse(header.OriginalActor.Id, out var userId):
                return await reader.HydrateAsync(new FirmProfessionalDutyDesignation(userId,
                    FirmProfessionalDuty.EngagementPartner, tenantId), ct).ConfigureAwait(false);
            case IndependenceRuleCatalog.Area:
                return await reader.HydrateAsync(new IndependenceRuleCatalog(), ct).ConfigureAwait(false);
            case IndependenceRuleRatificationCatalog.Area:
                return await reader.HydrateAsync(new IndependenceRuleRatificationCatalog(), ct).ConfigureAwait(false);
            default:
                return null;
        }
    }
}
