using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
namespace Bdgrz.Compliance.Features.AuditorRequests.Services;

static class AuditorResponseVersionNumber
{
    public static bool TryGet(long value, out int version)
    {
        version = 0;
        if (value is <= 0 or > int.MaxValue)
            return false;
        version = (int)value;
        return true;
    }
}
