using Cntryl.Portia;
using Bdgrz.Compliance.Features.Workforce.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Snapshots.Models;
namespace Bdgrz.Compliance.Features.Workforce.Commands;

/// <summary>
///     Freezes the current roster as an attributable amendment of an earlier roster snapshot. The
///     earlier snapshot is never changed.
/// </summary>
[Discriminator("bdgrz.snapshot.workforce_roster.amend", 1)]
public sealed record AmendWorkforceRosterSnapshot(Uuid TenantId, Uuid SnapshotId, string Reason)
    : IRequest<SnapshotRegistration>, IWorkforceRequest, IClientManagementMutationRequest, ICallable;
