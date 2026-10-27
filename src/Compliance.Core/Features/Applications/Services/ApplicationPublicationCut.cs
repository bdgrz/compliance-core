using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
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
using Bdgrz.Compliance.Features.SourceFacts.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Features.Applications.Services;

/// <summary>
///     One captured global frontier and the declared applications read at it. Each application replays only its own
///     stream facts below the cut; a held, applied target is shown as published exactly when the batch's own
///     <see cref="ApplicationImportCommitted" /> fact lies below the same cut. Nothing is appended and no live
///     aggregate is consulted, and an incomplete scan fails closed instead of reading as an absent fact.
/// </summary>
public sealed class ApplicationPublicationCut
{
    readonly IAuthoritativeSnapshotReader _snapshots;
    readonly Dictionary<(Uuid Tenant, Uuid Batch), bool> _published = [];

    internal ApplicationPublicationCut(IAuthoritativeSnapshotReader snapshots, AuthoritativeReadCut cut)
    {
        _snapshots = snapshots;
        Cut = cut;
    }

    /// <summary>The frontier shared by every read of this cut.</summary>
    public AuthoritativeReadCut Cut { get; }

    /// <summary>Replays the application's own facts below the cut and composes the publication its holds wait for.</summary>
    /// <exception cref="SourceFactVerificationException">The scan did not cover the cut completely.</exception>
    public async ValueTask<DeclaredApplication> ReadAsync(Uuid tenantId, Uuid applicationId,
        CancellationToken ct = default)
    {
        var scan = await _snapshots.ReadAtAsync(new SnapshotSelector(DeclaredApplication.Area, applicationId.ToString()),
            tenantId.ToString(), Cut, null, ct).ConfigureAwait(false);
        if (!scan.IsComplete)
            throw new SourceFactVerificationException("The application scan did not reach the cut.");
        var application = new DeclaredApplication(tenantId, applicationId);
        foreach (var fact in scan.Facts)
            application.Replay(fact.Event);
        foreach (var hold in application.ImportHolds.Where(hold => hold.IsActive && hold.IsApplied).ToArray())
            if (await IsPublishedAsync(tenantId, hold.BatchId, ct).ConfigureAwait(false))
                application.ShowPublished(hold.BatchId);
        return application;
    }

    async ValueTask<bool> IsPublishedAsync(Uuid tenantId, Uuid batchId, CancellationToken ct)
    {
        if (_published.TryGetValue((tenantId, batchId), out var known))
            return known;
        var scan = await _snapshots.ReadAtAsync(new SnapshotSelector(ApplicationImportBatch.Area, batchId.ToString()),
            tenantId.ToString(), Cut, null, ct).ConfigureAwait(false);
        if (!scan.IsComplete)
            throw new SourceFactVerificationException("The batch scan did not reach the cut.");
        var published = scan.Facts.Any(fact => fact.Event is ApplicationImportCommitted committed &&
                                               committed.BatchId == batchId);
        _published[(tenantId, batchId)] = published;
        return published;
    }
}
