using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Services;

/// <summary>Components, information assets, and data flows share one tenant stream area.</summary>
static class TechnologyInventoryStreams
{
    public const string Area = "technology-inventory";
    public const string ManualSource = "manual";

    public static EventStreamAddress Address(Uuid tenantId, Uuid recordId) =>
        new(tenantId.ToString(), Area, recordId.ToString());

    public static EventStreamPattern TenantPattern(Uuid tenantId) =>
        EventStreamPattern.ForPattern(tenantId.ToString(), Area);
}
