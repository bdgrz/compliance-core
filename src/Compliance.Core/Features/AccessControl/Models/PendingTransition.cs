using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

/// <summary>
///     An operation the guard accepted and has not settled: a withdrawal, a restoration that clears
///     <paramref name="Reason" />, or a first registration of the episode's administrators-team assignment.
/// </summary>
public sealed record PendingTransition(bool IsWithdrawal, Uuid Episode, string? Reason, long Generation,
    bool IsRegistration, bool Superseded = false)
{
    /// <summary>True when the source change lands on the member owner.</summary>
    public bool OwnedByMember => Reason is TenantManagerGuard.Suspended or TenantManagerGuard.Deprovisioned;

    /// <summary>True when the source change lands on the administrators-team assignment owner.</summary>
    public bool OwnedByAssignment => IsRegistration || Reason == TenantManagerGuard.RemovedFromAdministrators;
}
