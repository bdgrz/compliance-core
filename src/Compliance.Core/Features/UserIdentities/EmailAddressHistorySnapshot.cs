using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Transient complete address chronology and historical challenge fences.</summary>
public sealed record EmailAddressHistorySnapshot(IReadOnlyList<DomainEvent> Facts,
    IReadOnlyDictionary<Uuid, EmailAddress.RecoveryClaim> Claims, IReadOnlySet<Uuid> CompletedRecoveries);
