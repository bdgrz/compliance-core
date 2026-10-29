using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.SourceUse.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SourceUse.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>Resolves the source owner that holds one required source use of an engagement operation.</summary>
static class EngagementUseOwners
{
    /// <summary>
    ///     Hydrates the owner of <paramref name="stream" />, or returns null when the area is not a source use owner.
    ///     The duty designation is scoped by the original actor of the frozen header, who is the partner.
    /// </summary>
    public static async ValueTask<ISourceUseOwner?> HydrateAsync(IAggregateReader reader, EventStreamAddress stream,
        DurableIntentHeader header, Uuid tenantId, ReservationKey key, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(header);
        switch (stream.Area)
        {
            case ClientServiceIndependence.Area:
                return await reader.HydrateAsync(new ClientServiceIndependence(tenantId).ForSourceUses(key), ct).ConfigureAwait(false);
            case FirmStaffRecord.Area when Uuid.TryParse(stream.Resource, out var staffId):
                return await reader.HydrateAsync(new FirmStaffRecord(staffId).ForSourceUses(key), ct).ConfigureAwait(false);
            case FirmProfessionalDutyDesignation.Area when Uuid.TryParse(header.OriginalActor.Id, out var userId):
                return await reader.HydrateAsync(new FirmProfessionalDutyDesignation(userId,
                    FirmProfessionalDuty.EngagementPartner, tenantId).ForSourceUses(key), ct).ConfigureAwait(false);
            case IndependenceRuleCatalog.Area:
                return await reader.HydrateAsync(new IndependenceRuleCatalog().ForSourceUses(key), ct).ConfigureAwait(false);
            case IndependenceRuleRatificationCatalog.Area:
                return await reader.HydrateAsync(new IndependenceRuleRatificationCatalog().ForSourceUses(key), ct).ConfigureAwait(false);
            default:
                return null;
        }
    }
}
