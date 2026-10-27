using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.Tenants.Models;
namespace Bdgrz.Compliance.Features.Tenants.Services;

static class TenantDirectoryStatus
{
    public static string? After(TenantView current, DomainEvent domainEvent)
    {
        if (current.Status == "offboarding")
            return "offboarding";
        if (domainEvent is (TenantActivated or TenantActivatedV2) && current.Status == "suspended")
            return "suspended";

        return domainEvent switch
        {
            TenantSlugConfirmed when current.RequiresActivation => "provisioning",
            TenantSlugConfirmed or TenantReactivated or TenantReactivatedV2 or TenantActivated or TenantActivatedV2 => "active",
            TenantSlugRejected => "rejected",
            TenantSuspended or TenantSuspendedV2 => "suspended",
            TenantOffboardingStarted or TenantOffboardingStartedV2 => "offboarding",
            _ => null,
        };
    }
}
