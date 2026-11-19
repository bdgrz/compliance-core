using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Bdgrz.Compliance.Features.Work.Aggregates;
using Bdgrz.Compliance.Features.Work.Readers;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work.Services;

/// <summary>Records one item-derived escalation after re-reading the current open source work.</summary>
public sealed class WorkEscalationSweep(ITenantDirectoryReader tenants, ITenantActivity activity,
    IProgramDirectoryReader programs, WorkQueueReader queue, IAggregateExecutor executor, TimeProvider clock)
{
    public const string ProcessId = "work-overdue-escalation";

    public async ValueTask RunOnceAsync(CancellationToken ct)
    {
        string? cursor = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        do
        {
            var page = await tenants.ListAsync(200, cursor, ct).ConfigureAwait(false);
            foreach (var tenant in page.Items)
            {
                if (tenant.Status != "active" || !await activity.IsActiveAsync(tenant.TenantId, ct).ConfigureAwait(false))
                    continue;
                string? programCursor = null;
                var programCursors = new HashSet<string>(StringComparer.Ordinal);
                do
                {
                    var programPage = await programs.ListAsync(tenant.TenantId, 200, programCursor, ct).ConfigureAwait(false);
                    foreach (var program in programPage.Items)
                    {
                        if (program.TenantId != tenant.TenantId)
                            throw new InvalidOperationException("The program directory returned another tenant's work scope.");
                        await ProcessProgramAsync(tenant.TenantId, program.ProgramId, ct).ConfigureAwait(false);
                    }
                    programCursor = programPage.NextCursor;
                    if (programCursor is not null && !programCursors.Add(programCursor))
                        throw new InvalidOperationException("The program directory repeated an escalation cursor.");
                } while (programCursor is not null);
            }
            cursor = page.NextCursor;
            if (cursor is not null && !seen.Add(cursor))
                throw new InvalidOperationException("The tenant directory repeated an escalation cursor.");
        } while (cursor is not null);
    }

    internal async ValueTask ProcessProgramAsync(Uuid tenantId, Uuid programId, CancellationToken ct)
    {
        var found = await queue.ReadForEscalationAsync(tenantId, programId, null, ct).ConfigureAwait(false);
        if (!found.IsSuccess)
            return;
        foreach (var candidate in found.Value.Entries)
        {
            if (candidate.State.Escalated || candidate.Candidate.DueOn is not { } due ||
                found.Value.Today.DayNumber - due.DayNumber < WorkQueueReader.SystemEscalationDays)
                continue;
            var current = await queue.ReadForEscalationAsync(tenantId, programId, candidate.Item.WorkItemId, ct).ConfigureAwait(false);
            if (!current.IsSuccess || current.Value.Entries.Count != 1)
                continue;
            var entry = current.Value.Entries[0];
            if (entry.State.Escalated || entry.Candidate.DueOn is not { } currentDue ||
                current.Value.Today.DayNumber - currentDue.DayNumber < WorkQueueReader.SystemEscalationDays)
                continue;
            var commandId = WorkItemAssignment.AutomaticEscalationId(programId, entry.Item.WorkItemId);
            var request = new AutomaticEscalation(commandId, tenantId, programId, entry.Item.WorkItemId);
            var now = clock.GetUtcNow();
            try
            {
                _ = await executor.ExecuteAsync(new WorkItemAssignment(tenantId, programId, entry.Item.WorkItemId),
                    owner =>
                    {
                        var changed = owner.EscalateAutomatically(commandId, entry.State.Revision,
                            entry.Candidate.SourceId, entry.Candidate.Kind, currentDue, current.Value.Today, now);
                        return changed.IsSuccess && changed.Value
                            ? AggregateOutcome.Commit(changed) : AggregateOutcome.Discard(changed);
                    }, new RequestContext<AutomaticEscalation>(request, RequestActor.System), ct).ConfigureAwait(false);
            }
            catch (EventStreamConcurrencyException)
            {
                // The next scheduler pass re-reads the source and the winner's current assignment.
            }
        }
    }

    sealed record AutomaticEscalation(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid WorkItemId) : IRequest;
}
