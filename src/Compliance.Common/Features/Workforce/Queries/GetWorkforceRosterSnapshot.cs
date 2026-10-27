using Cntryl.Portia;
using Bdgrz.Compliance.Features.Workforce.Contracts;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Workforce.Queries;

/// <summary>Reads a frozen roster; the manager chain is returned only with its field permission.</summary>
[Discriminator("bdgrz.snapshot.workforce_roster.get", 1)]
public sealed record GetWorkforceRosterSnapshot(Uuid TenantId, Uuid SnapshotId)
    : IRequest<WorkforceRosterSnapshotView>, IWorkforceRequest, ICallable;
