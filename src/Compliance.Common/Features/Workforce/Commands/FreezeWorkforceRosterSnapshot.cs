using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Features.Snapshots.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Commands;

/// <summary>Freezes the tenant's accepted people and work relationships as an immutable roster snapshot.</summary>
[Discriminator("bdgrz.snapshot.workforce_roster.freeze", 1)]
public sealed record FreezeWorkforceRosterSnapshot(Uuid TenantId)
    : IRequest<SnapshotRegistration>, IWorkforceRequest, IClientManagementMutationRequest, ICallable;
