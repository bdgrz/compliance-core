using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Contracts;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Workforce.Projections;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Snapshots.Events;
using Bdgrz.Compliance.Features.Snapshots.Models;
using Bdgrz.Compliance.Features.Snapshots.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Examinations.Services;

/// <summary>Resolves the latest intact workforce roster frozen on or before the Type I date.</summary>
public sealed class WorkforceTypeIBaselineSourceContributor(
    IPopulationSnapshotDirectoryReader snapshots, IAggregateReader reader,
    PopulationSnapshotDirectoryReadConsistency consistency)
    : ITypeIBaselineSourceContributor
{
    const int PageSize = 200;
    const string SourceKind = "workforce_roster_snapshot";

    public string Family => "workforce";

    public ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        ExaminationEngagementView engagement, CancellationToken ct) =>
        ReadAsync(engagement, new TypeIBaselineSourceReadContext(Uuid.Empty, []), ct);

    public async ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        ExaminationEngagementView engagement, TypeIBaselineSourceReadContext context, CancellationToken ct)
    {
        var fence = await consistency.CaptureAsync(engagement.TenantId, ct).ConfigureAwait(false);
        if (!fence.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(fence.Error);

        var through = new DateTimeOffset(engagement.PeriodStart.ToDateTime(TimeOnly.MaxValue,
            DateTimeKind.Utc));
        PopulationSnapshotSummary? selected = null;
        string? cursor = null;
        var seenCursors = new HashSet<string>(StringComparer.Ordinal);
        do
        {
            var page = await snapshots.ListAsync(engagement.TenantId,
                WorkforceRosterSnapshotContent.Kind, PageSize, cursor, ct).ConfigureAwait(false);
            foreach (var item in page.Items)
            {
                if (item.TenantId != engagement.TenantId ||
                    item.Kind != WorkforceRosterSnapshotContent.Kind ||
                    item.SnapshotId == Uuid.Empty)
                    return InvalidDirectory();
                if (item.FrozenAt <= through && (context.RecordedThrough is null ||
                    (await context.RequireReceipts(engagement.TenantId).ReadAsync(new EventStreamAddress(
                        engagement.TenantId.ToString(), "population-snapshots", item.SnapshotId.ToString()), ct)
                        .ConfigureAwait(false)).Includes(fact => fact is PopulationSnapshotFrozen frozen &&
                            frozen.SnapshotId == item.SnapshotId && frozen.ContentSha256 == item.ContentSha256)))
                {
                    selected = item;
                    break;
                }
            }
            if (selected is not null)
                break;
            cursor = page.NextCursor;
            if (cursor is not null && !seenCursors.Add(cursor))
                return InvalidDirectory();
        } while (cursor is not null);

        var sources = new List<TypeIBaselineSourceReference>(1);
        var blockers = new List<string>();
        if (selected is null)
        {
            blockers.Add("No frozen workforce roster snapshot is available on or before the Type I date.");
        }
        else
        {
            var content = await PopulationSnapshotContent.ReadAsync(reader, engagement.TenantId,
                selected.SnapshotId, WorkforceRosterSnapshotContent.Kind, ct).ConfigureAwait(false);
            if (!content.IsSuccess || content.Value.Snapshot.RootSnapshotId != selected.RootSnapshotId ||
                content.Value.Snapshot.AmendsSnapshotId != selected.AmendsSnapshotId ||
                content.Value.Snapshot.RowCount != selected.RowCount ||
                content.Value.Snapshot.ContentSha256 != selected.ContentSha256 ||
                content.Value.Snapshot.FrozenAt != selected.FrozenAt)
            {
                blockers.Add("The selected workforce roster snapshot failed integrity verification.");
            }
            else
            {
                sources.Add(new TypeIBaselineSourceReference(SourceKind, selected.RootSnapshotId,
                    selected.SnapshotId, 1, selected.ContentSha256));
            }
        }

        var confirmed = await consistency.ConfirmUnchangedAndCaughtUpAsync(engagement.TenantId,
            fence.Value, ct).ConfigureAwait(false);
        if (!confirmed.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(confirmed.Error);

        return Result<TypeIBaselineSourceContribution>.Success(new TypeIBaselineSourceContribution(
            Family, sources.AsReadOnly(), blockers.AsReadOnly(), []));
    }

    static Result<TypeIBaselineSourceContribution> InvalidDirectory() =>
        Result<TypeIBaselineSourceContribution>.Failure(new RequestError(RequestErrorKind.Conflict,
            "The workforce roster snapshot directory returned an invalid or unstable page."));
}
