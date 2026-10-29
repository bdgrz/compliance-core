using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Projections;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Readers;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Readers;

/// <summary>
///     Serves the "TeamMemberDirectory" projector: writes join its batch transaction, and
///     query-side reads open their own read-only transaction on the resource it writes for the
///     given tenant.
/// </summary>
sealed class FitzTeamMemberDirectoryReader(IKvClient client, IDomainEventReader events)
    : FitzKvProjectionStore(client, TeamMemberDirectoryKeys.Route, "TeamMemberDirectory"),
      ITeamMemberDirectoryProjection,
      ITeamMemberDirectoryReader
{
    readonly IKvClient _client = client;

    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case TeamMemberAssigned assigned:
                await TeamMemberDirectorySchema.Directory.InsertAsync(
                    Transaction,
                    new TeamMemberView(assigned.TeamId, assigned.MemberId),
                    ct).ConfigureAwait(false);
                break;
            case TeamMemberRemoved removed:
                await TeamMemberDirectorySchema.Directory.DeleteAsync(
                    Transaction,
                    (removed.TeamId, removed.MemberId),
                    ct).ConfigureAwait(false);
                break;
        }
    }

    public async ValueTask<Page<TeamMemberView>> ListAsync(
        Uuid tenantId,
        Uuid teamId,
        int? limit,
        string? cursor,
        string? search,
        bool descending,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        if (search is null)
        {
            var query = TeamMemberDirectorySchema.ByTeam.Query().WithPrefix(teamId.ToString()).After(cursor);
            if (limit is not null)
            {
                query = query.Take(limit.Value);
            }

            if (descending)
            {
                query = query.Descending();
            }

            return await TeamMemberDirectorySchema.Directory.QueryAsync(tx, query, ct).ConfigureAwait(false);
        }

        return await SearchAsync(tx, teamId, limit, cursor, search, descending, ct).ConfigureAwait(false);
    }

    public async ValueTask<bool> HasPendingRemovalAsync(Uuid tenantId, Uuid teamId,
        Uuid memberId, CancellationToken ct = default)
    {
        var pattern = EventStreamPattern.ForPattern(tenantId.ToString());
        var checkpoint = await base.LoadCheckpointAsync(new CheckpointIdentity(
                "TeamMemberDirectory", pattern), ct)
            .ConfigureAwait(false);
        var isMember = true;
        await foreach (var record in events.ReadAsync(pattern, checkpoint.Cursor, ct)
                           .ConfigureAwait(false))
        {
            switch (record.Event)
            {
                case TeamMemberRemoved removed when removed.TenantId == tenantId &&
                    removed.TeamId == teamId && removed.MemberId == memberId:
                    isMember = false;
                    break;
                case TeamMemberAssigned assigned when assigned.TenantId == tenantId &&
                    assigned.TeamId == teamId && assigned.MemberId == memberId:
                    isMember = true;
                    break;
            }
        }
        return !isMember;
    }

    public async ValueTask<IReadOnlyList<TeamMemberView>> ListMemberAssignmentsAsync(
        Uuid tenantId, Uuid memberId, CancellationToken ct = default)
    {
        string route;
        await using (var routeTx = await BeginReadAsync(tenantId.ToString(), ct)
                         .ConfigureAwait(false))
            route = routeTx.Route;
        string? backfillCursor = null;
        do
        {
            var batch = await TeamMemberDirectorySchema.Directory.BackfillAsync(_client, route,
                TeamMemberDirectorySchema.ByMember, 200, KvDurability.Sync, backfillCursor, ct)
                .ConfigureAwait(false);
            backfillCursor = batch.NextCursor;
        } while (backfillCursor is not null);

        var assignments = new Dictionary<Uuid, TeamMemberView>();
        await using (var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false))
        {
            string? cursor = null;
            do
            {
                var page = await TeamMemberDirectorySchema.Directory.QueryAsync(tx,
                    TeamMemberDirectorySchema.ByMember.Query()
                        .WithPrefix(memberId.ToString()).Take(200).After(cursor), ct)
                    .ConfigureAwait(false);
                foreach (var item in page.Items)
                    assignments[item.TeamId] = item;
                cursor = page.NextCursor;
            } while (cursor is not null);
        }

        var pattern = EventStreamPattern.ForPattern(tenantId.ToString());
        var checkpoint = await base.LoadCheckpointAsync(new CheckpointIdentity(
                "TeamMemberDirectory", pattern), ct).ConfigureAwait(false);
        await foreach (var record in events.ReadAsync(pattern, checkpoint.Cursor, ct)
                           .ConfigureAwait(false))
        {
            switch (record.Event)
            {
                case TeamMemberAssigned assigned when assigned.TenantId == tenantId &&
                    assigned.MemberId == memberId:
                    assignments[assigned.TeamId] = new TeamMemberView(assigned.TeamId,
                        assigned.MemberId);
                    break;
                case TeamMemberRemoved removed when removed.TenantId == tenantId &&
                    removed.MemberId == memberId:
                    assignments.Remove(removed.TeamId);
                    break;
            }
        }
        return assignments.Values.OrderBy(static item => item.TeamId.ToString(),
            StringComparer.Ordinal).ToArray();
    }

    // Same reasoning as FitzTeamDirectoryReader.SearchAsync: the by_team index prefix only bounds
    // the scan to one team, it cannot substring-match member IDs, so a real search walks that team's
    // member-id-ordered range one entry at a time and filters here. See
    // design-api-contracts-hide-impl-strategy — Search means substring, not "whatever the index can
    // serve directly."
    static async ValueTask<Page<TeamMemberView>> SearchAsync(
        IKvTransaction tx, Uuid teamId, int? limit, string? cursor, string search, bool descending, CancellationToken ct)
    {
        var effectiveLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var matches = new List<TeamMemberView>();
        var scanCursor = cursor;
        while (matches.Count < effectiveLimit)
        {
            var step = TeamMemberDirectorySchema.ByTeam.Query().WithPrefix(teamId.ToString()).Take(1).After(scanCursor);
            if (descending)
            {
                step = step.Descending();
            }

            var page = await TeamMemberDirectorySchema.Directory.QueryAsync(tx, step, ct).ConfigureAwait(false);
            if (page.Items.Count == 0)
            {
                scanCursor = null;
                break;
            }

            if (page.Items[0].MemberId.ToString().Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(page.Items[0]);
            }

            scanCursor = page.NextCursor;
            if (scanCursor is null)
            {
                break;
            }
        }

        return new Page<TeamMemberView>(matches, matches.Count == effectiveLimit ? scanCursor : null);
    }
}
