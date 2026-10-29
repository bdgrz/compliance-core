using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
namespace Bdgrz.Compliance.Features.AuditorRequests.Services;

static class AuditorRequestOwner
{
    public static async ValueTask<bool> IsActiveClientMemberAsync(IAggregateReader reader,
        Uuid tenantId, Uuid memberId, CancellationToken ct)
    {
        if (tenantId == Uuid.Empty || memberId == Uuid.Empty)
            return false;
        var member = await reader.HydrateAsync(Member.ForVerification(tenantId, memberId), ct)
            .ConfigureAwait(false);
        return member.IsRegistered && !member.IsSuspended && !member.IsDeprovisioned &&
               member.Affiliation == "client_personnel";
    }

    public static RequestError InvalidOwner() => new(RequestErrorKind.Validation,
        "The auditor request owner must be an active client member.");
}
