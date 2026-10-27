using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

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
