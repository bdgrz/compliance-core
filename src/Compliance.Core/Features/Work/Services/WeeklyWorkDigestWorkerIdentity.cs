using System.Security.Claims;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
namespace Bdgrz.Compliance.Features.Work.Services;

static class WeeklyWorkDigestWorkerIdentity
{
    public static bool IsNamed(IRequestContext context, string processId)
    {
        if (!RequestActor.IsSystem(context.Actor) || context.Invocation is not DirectInvocation)
            return false;
        var subjects = context.Actor.FindAll(ClaimTypes.NameIdentifier).ToArray();
        return subjects.Length == 1 && subjects[0].Issuer == "bdgrz.system" &&
               subjects[0].Value == processId;
    }
}
