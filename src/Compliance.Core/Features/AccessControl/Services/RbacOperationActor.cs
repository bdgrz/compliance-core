using System.Security.Claims;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
namespace Bdgrz.Compliance.Features.AccessControl.Services;

static class RbacOperationActor
{
    public static ActorAttribution From(ClaimsPrincipal principal)
    {
        var identity = principal.Identities.FirstOrDefault(item => item.IsAuthenticated);
        var subject = identity?.FindFirst(ClaimTypes.NameIdentifier) ?? identity?.FindFirst("sub");
        return subject is null || string.IsNullOrWhiteSpace(subject.Value) || string.IsNullOrWhiteSpace(subject.Issuer)
            ? throw new InvalidOperationException("RBAC authorization must establish a stable actor.")
            : new ActorAttribution(subject.Value, subject.Issuer);
    }
}
