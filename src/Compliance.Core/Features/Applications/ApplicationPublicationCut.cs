using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

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
