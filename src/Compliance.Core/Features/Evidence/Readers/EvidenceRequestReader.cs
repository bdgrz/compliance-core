using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Projections;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Events;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Evidence.Readers;

/// <summary>
///     Reads evidence requests from their own owner streams. Every request of a program is discovered from the opening
///     facts of the tenant and hydrated from its own stream, so nothing is read from a program-wide ledger and nothing is
///     retained as command state.
/// </summary>
public sealed class EvidenceRequestReader(IAggregateReader reader, IDomainEventReader events)
{
    /// <summary>The view of one request, or null when it was not opened.</summary>
    public async ValueTask<EvidenceRequestView?> ReadViewAsync(Uuid tenantId, Uuid programId, Uuid requestId,
        CancellationToken ct = default) =>
        (await reader.HydrateAsync(new EvidenceRequest(tenantId, programId, requestId), ct).ConfigureAwait(false))
        .View();

    /// <summary>Every request of the program, in the order opened.</summary>
    public async ValueTask<EvidenceRequestSnapshot> ReadAsync(Uuid tenantId, Uuid programId,
        CancellationToken ct = default)
    {
        var opened = new List<(DateTimeOffset At, Uuid Id)>();
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(),
                           EvidenceRequest.Area), EventCursor.Start, ct).ConfigureAwait(false))
            if (!record.Event.Metadata.IsAudit && record.Event is EvidenceRequestOpened ev &&
                ev.TenantId == tenantId && ev.ProgramId == programId)
                opened.Add((ev.OpenedAt, ev.EvidenceRequestId));

        var views = new List<EvidenceRequestView>();
        foreach (var (_, id) in opened.OrderBy(static item => item.At).ThenBy(static item => item.Id.ToString(),
                     StringComparer.Ordinal))
            if (await ReadViewAsync(tenantId, programId, id, ct).ConfigureAwait(false) is { } view)
                views.Add(view);
        return new EvidenceRequestSnapshot(views);
    }
}
