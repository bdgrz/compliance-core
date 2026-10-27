using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.Remediation.Readers;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Work.Services;

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
