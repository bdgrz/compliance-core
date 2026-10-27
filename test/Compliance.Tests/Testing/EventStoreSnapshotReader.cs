using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Fixed-cut reader over an in-memory event store that reports one frontier above every record it holds. It only
///     serves concrete resource selectors, because the store reads one stream at a time; the broker-shaped scans are
///     covered by the scripted stream client tests of the real reader.
/// </summary>
sealed class EventStoreSnapshotReader(IDomainEventReader events) : IAuthoritativeSnapshotReader
{
    public const ulong Frontier = EventStoreSourceFactResolver.GlobalBase * 10;

    public Task<SnapshotCapture> CaptureAsync(CancellationToken ct = default) => Task.FromResult(new SnapshotCapture(
        new AuthoritativeReadCut("stream://**", Frontier, DateTimeOffset.UnixEpoch), 1));

    public async Task<SnapshotReadResult> ReadAtAsync(SnapshotSelector selector, string tenantScope,
        AuthoritativeReadCut cut, SnapshotCursor? cursor = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(selector);
        if (cursor is not null && (cursor.Route != selector.Route || cursor.TenantScope != tenantScope ||
            cursor.CutWatermarkExclusive != cut.GlobalWatermarkExclusive))
            throw new SourceFactVerificationException("A cursor belongs to its original tenant, selector and cut.");
        var resource = selector.Resource ?? throw new SourceFactVerificationException(
            "The in-memory reader serves concrete resources only.");
        var stream = new EventStreamAddress(tenantScope, selector.Area, resource);
        var facts = new List<ResolvedSourceFact>();
        try
        {
            await foreach (var record in events.ReadAsync(stream, 0, ct))
            {
                if (record.Event.Metadata is { IsAudit: true })
                    continue;
                var global = EventStoreSourceFactResolver.GlobalBase + record.ResourceOffset;
                if (global >= cut.GlobalWatermarkExclusive)
                    break;
                facts.Add(SourceFactVerifier.Build(stream, record.ResourceOffset,
                    EventStoreSourceFactResolver.RealmBase + record.ResourceOffset, global, record.Event));
            }
        }
        catch (InvalidOperationException)
        {
            // The in-memory store reports an unknown stream this way: nothing is recorded below the cut.
        }

        return new SnapshotReadResult(facts, new SnapshotCursor(selector.Route, cut.GlobalWatermarkExclusive, null,
            cut.GlobalWatermarkExclusive, tenantScope, cut.GlobalWatermarkExclusive), true);
    }
}
