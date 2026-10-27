using Bdgrz.Compliance.Features.UserIdentities.Events;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Transient complete identity history, never retained by a command owner.</summary>
public sealed record UserIdentityHistorySnapshot(IReadOnlyList<DomainEvent> Facts,
    IReadOnlyDictionary<Uuid, RecoveryIdentityProtected> Protections, IReadOnlySet<Uuid> SettledChallenges);
