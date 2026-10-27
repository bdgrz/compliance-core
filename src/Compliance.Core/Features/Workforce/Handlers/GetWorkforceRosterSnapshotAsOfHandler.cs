using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Workforce.Projections;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Snapshots.Readers;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Queries;
namespace Bdgrz.Compliance.Features.Workforce.Handlers;

/// <summary>
///     Finds the latest roster snapshot frozen at or before the instant through the snapshot
///     directory, then reads it from its immutable source. A just-frozen snapshot may not be found
///     until the directory projection catches up.
/// </summary>
public sealed class GetWorkforceRosterSnapshotAsOfHandler(
    IPopulationSnapshotDirectoryReader directory, IAggregateReader reader,
    IPermissionAuthorizer permissions)
    : IRequestHandler<GetWorkforceRosterSnapshotAsOf, WorkforceRosterSnapshotView>
{
    public async ValueTask<Result<WorkforceRosterSnapshotView>> HandleAsync(
        IRequestContext<GetWorkforceRosterSnapshotAsOf> context, CancellationToken ct)
    {
        var request = context.Request;
        string? cursor = null;
        do
        {
            var page = await directory.ListAsync(request.TenantId,
                WorkforceRosterSnapshotContent.Kind, 200, cursor, ct).ConfigureAwait(false);
            var match = page.Items.FirstOrDefault(item =>
                item.TenantId == request.TenantId && item.FrozenAt <= request.AsOf);
            if (match is not null)
                return await WorkforceRosterSnapshotReads.ReadAsync(context, reader, permissions,
                    request.TenantId, match.SnapshotId, ct).ConfigureAwait(false);
            cursor = page.NextCursor;
        } while (cursor is not null);
        return Result<WorkforceRosterSnapshotView>.Failure(new RequestError(
            RequestErrorKind.NotFound, "No roster snapshot was frozen at or before that time."));
    }
}
