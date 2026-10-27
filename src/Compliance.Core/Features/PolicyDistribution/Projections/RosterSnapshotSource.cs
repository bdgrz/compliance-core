using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Workforce.Projections;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Projections;

/// <summary>
///     Reads an accepted, integrity-verified frozen workforce roster. Campaigns use only identity,
///     worker type, status, start date, and roster team, so the restricted manager chain stays
///     redacted.
/// </summary>
public static class RosterSnapshotSource
{
    public static async ValueTask<Result<(WorkforceRosterSnapshotView Roster, DateTimeOffset FrozenAt)>>
        ReadAsync(IAggregateReader reader, Uuid tenantId, Uuid snapshotId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var content = await PopulationSnapshotContent.ReadAsync(reader, tenantId, snapshotId,
            WorkforceRosterSnapshotContent.Kind, ct).ConfigureAwait(false);
        if (!content.IsSuccess)
            return Result<(WorkforceRosterSnapshotView, DateTimeOffset)>.Failure(new RequestError(
                content.Error.Kind, content.Error.Kind == RequestErrorKind.NotFound
                    ? "The roster snapshot was not found."
                    : "The stored roster snapshot failed integrity verification."));
        var snapshot = content.Value.Snapshot;
        return Result<(WorkforceRosterSnapshotView, DateTimeOffset)>.Success((
            WorkforceRosterSnapshotContent.ToView(tenantId, snapshot, content.Value.Rows,
                new FieldRedactor(FieldClasses.WorkforceManagerChain, false)),
            snapshot.FrozenAt));
    }
}
