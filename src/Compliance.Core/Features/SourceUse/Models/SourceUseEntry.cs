using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Services;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.SourceUse.Models;

/// <summary>
///     One slot of <see cref="SourceUseReservations" />: the grant fact and its receiver generation, and the
///     settlement fact once it exists. A slot with a settlement and no grant is a fence.
/// </summary>
public sealed record SourceUseEntry(ulong ReservationGeneration, DomainEvent? GrantFact, DomainEvent? SettleFact)
{
    /// <summary>True when granted and not settled.</summary>
    public bool IsActive => GrantFact is not null && SettleFact is null;

    /// <summary>True once settled, with or without a grant.</summary>
    public bool IsSettled => SettleFact is not null;

    /// <summary>True when the key was settled before any grant: late acquisition is refused.</summary>
    public bool IsFence => GrantFact is null && SettleFact is not null;
}
