using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>
///     Resolves which finding a committed fact of the finding or corrective action owners concerns. Finding work is
///     derived from the authoritative owners at projection time, so every fact that can change it names its finding and
///     nothing else changes it.
/// </summary>
static class FindingWorkScope
{
    public static async ValueTask<(Uuid TenantId, Uuid ProgramId, Uuid FindingId)?> OfAsync(DomainEvent fact,
        FindingReader findings, CancellationToken ct)
    {
        switch (fact)
        {
            case FindingRaisedV2 ev:
                return (ev.TenantId, ev.ProgramId, ev.FindingId);
            case FindingRevisedV2 ev:
                return (ev.TenantId, ev.ProgramId, ev.FindingId);
            case FindingAcceptanceLinkedV2 ev:
                return (ev.TenantId, ev.ProgramId, ev.FindingId);
            case FindingClosedV2 ev:
                return (ev.TenantId, ev.ProgramId, ev.FindingId);
            case FindingReopenedV2 ev:
                return (ev.TenantId, ev.ProgramId, ev.FindingId);
            case CorrectiveActionAddedV2 ev:
                return (ev.TenantId, ev.ProgramId, ev.FindingId);
            case CorrectiveActionCompletedV2 ev:
                return (ev.TenantId, ev.ProgramId, ev.FindingId);
            case CorrectiveActionAdmitted ev:
                var tenant = Uuid.Parse(ev.Owner.Realm, System.Globalization.CultureInfo.InvariantCulture);
                var (intent, authored) = await findings.ReadAdmissionAsync(tenant, ev, ct).ConfigureAwait(false);
                return (tenant, authored.ProgramId, intent.ParentId);
            default:
                return null;
        }
    }
}
