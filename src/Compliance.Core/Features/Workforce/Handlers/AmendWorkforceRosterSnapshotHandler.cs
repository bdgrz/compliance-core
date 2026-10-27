using Cntryl.Portia;
using Bdgrz.Compliance.Features.Workforce.Projections;
using Bdgrz.Compliance.Features.Snapshots.Models;
using Bdgrz.Compliance.Features.Workforce.Commands;
namespace Bdgrz.Compliance.Features.Workforce.Handlers;

public sealed class AmendWorkforceRosterSnapshotHandler(WorkforceRosterSnapshotter snapshotter)
    : IRequestHandler<AmendWorkforceRosterSnapshot, SnapshotRegistration>
{
    public ValueTask<Result<SnapshotRegistration>> HandleAsync(
        IRequestContext<AmendWorkforceRosterSnapshot> context, CancellationToken ct) =>
        snapshotter.FreezeAsync(context, context.Request.SnapshotId,
            context.Request.Reason ?? string.Empty, ct);
}
