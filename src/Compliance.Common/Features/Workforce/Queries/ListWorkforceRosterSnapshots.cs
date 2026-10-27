using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Workforce.Contracts;
using Bdgrz.Compliance.Features.Snapshots.Models;
namespace Bdgrz.Compliance.Features.Workforce.Queries;

/// <summary>Lists roster snapshot identities and lineage, newest first, without roster rows.</summary>
[Discriminator("bdgrz.snapshot.workforce_roster.list", 1)]
public sealed record ListWorkforceRosterSnapshots(Uuid TenantId, int? Limit = null,
    string? Cursor = null)
    : IRequest<Page<PopulationSnapshotSummary>>, IWorkforceRequest, ICallable;
