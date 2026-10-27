using Cntryl.Portia;
using Bdgrz.Compliance.Features.Workforce.Projections;
using Bdgrz.Compliance.Features.Snapshots.Models;
using Bdgrz.Compliance.Features.Workforce.Commands;
namespace Bdgrz.Compliance.Features.Workforce.Handlers;

public sealed class FreezeWorkforceRosterSnapshotHandler(WorkforceRosterSnapshotter snapshotter)
    : IRequestHandler<FreezeWorkforceRosterSnapshot, SnapshotRegistration>
{
    public ValueTask<Result<SnapshotRegistration>> HandleAsync(
        IRequestContext<FreezeWorkforceRosterSnapshot> context, CancellationToken ct) =>
        snapshotter.FreezeAsync(context, null, null, ct);
}
