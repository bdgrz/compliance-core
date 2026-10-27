using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
namespace Bdgrz.Compliance.Features.UserIdentities.Projections;

/// <summary>Transient complete address chronology and historical challenge fences.</summary>
public sealed record EmailAddressHistorySnapshot(IReadOnlyList<DomainEvent> Facts,
    IReadOnlyDictionary<Uuid, EmailAddress.RecoveryClaim> Claims, IReadOnlySet<Uuid> CompletedRecoveries);
