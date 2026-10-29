using Cntryl.Portia;
using Bdgrz.Compliance.Features.Workforce.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Snapshots.Models;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Workforce.Commands;

/// <summary>Freezes the tenant's accepted people and work relationships as an immutable roster snapshot.</summary>
[Discriminator("bdgrz.snapshot.workforce_roster.freeze", 1)]
public sealed record FreezeWorkforceRosterSnapshot(Uuid TenantId)
    : IRequest<SnapshotRegistration>, IWorkforceRequest, IClientManagementMutationRequest, ICallable;
