using Bdgrz.Compliance.Features.Programs.Readers;
using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Contracts;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Boundaries.Projections;
using Bdgrz.Compliance.Features.Boundaries.Services;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Boundaries.Events;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Boundaries.Readers;
namespace Bdgrz.Compliance.Features.Boundaries.Readers;

sealed class CassieBoundaryDirectory(CassieProjectionDatabase database, BoundaryReader boundaries)
    : CassieProgramScopeRepository(database, ProjectorName),
        IBoundaryDirectoryReader, IBoundaryDirectoryProjection
{
    public const string ProjectorName = "BoundaryDirectoryV3";
    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(ProjectorName, EventStreamPattern.ForPattern(tenantId.ToString())), ct);

    public async ValueTask ApplyAsync(DomainEvent value, CancellationToken ct = default)
    {
        value = BoundaryFactShape.Normalize(value);
        if (value is not IBoundaryFact fact || value is not (BoundaryDraftCreated or BoundaryDraftRevised or BoundaryDraftDiscarded or
            BoundaryReviewed or BoundaryApproved or BoundarySuccessorProposed))
            return;
        BoundaryFactContent.ValidateScope(fact);
        var header = fact.Header;
        if (Identity.Pattern != EventStreamPattern.ForPattern(header.TenantId.ToString()) || fact.ChangeCount != 1 || fact.NativeSequence < 1 || header.Revision < 1 || !header.IsCreated)
            throw new InvalidOperationException("A boundary directory fact requires its exact native owner and batch tenant.");
        var current = await CurrentBoundaryAsync( header.BoundaryId, ct).ConfigureAwait(false);
        if (current is not null && current.Header == header && current.NativeSequence == fact.NativeSequence)
            return;
        if (value is BoundaryDraftCreated)
        {
            if (current is not null || header.Revision != 1 || fact.NativeSequence != 1 || header.DraftRevision != 1)
                throw new InvalidOperationException("A boundary cannot project before its exact original creation.");
        }
        else if (current is null || current.Header.TenantId != header.TenantId || current.Header.ProgramId != header.ProgramId ||
            header.Revision != current.Header.Revision + 1 || fact.NativeSequence <= current.NativeSequence)
            throw new InvalidOperationException("A boundary mutation cannot project before its exact predecessor.");
        switch (value)
        {
            case BoundaryDraftRevised revised when current!.Header.DraftVersionId != revised.DraftVersionId ||
                current.Header.DraftRevision + 1 != revised.Revision:
                throw new InvalidOperationException("A boundary draft revision cannot project before its creation.");
            case BoundaryDraftDiscarded discarded when current!.Header.DraftVersionId != discarded.DraftVersionId ||
                current.Header.DraftRevision != discarded.Revision:
                throw new InvalidOperationException("A draft discard cannot project before its exact draft revision.");
            case BoundaryReviewed reviewed when current!.Header.DraftVersionId != reviewed.DraftVersionId ||
                current.Header.DraftRevision != reviewed.Revision:
                throw new InvalidOperationException("A boundary review cannot project before its exact draft revision.");
            case BoundaryApproved approved when current!.Header.DraftVersionId != approved.DraftVersionId ||
                current.Header.DraftRevision != approved.Revision || current.Header.AcceptedReviewDecisionId != approved.AcceptedReviewDecisionId:
                throw new InvalidOperationException("A boundary approval cannot project before its accepted review.");
            case BoundaryApproved scheduled when current!.Header.LatestApprovedEffectiveFrom is { } prior && scheduled.EffectiveFrom <= prior:
                throw new InvalidOperationException("A successor boundary version must become effective after its predecessor.");
            case BoundarySuccessorProposed proposed when current!.Header.LatestApprovedVersionId != proposed.PredecessorVersionId ||
                current.Header.DraftVersionId != Uuid.Empty:
                throw new InvalidOperationException("A successor cannot project before its approved predecessor.");
        }
        var row = new BoundaryDirectoryIdentity(header, fact.NativeSequence);
        if (current is null)
            await InsertIdentityAsync("boundary_current_identities", row, ct).ConfigureAwait(false);
        else
            await ReplaceIdentityAsync(row, ct).ConfigureAwait(false);
        if (value is BoundaryReviewed review)
            await InsertDecisionAsync(
                row with { DecisionId = review.DecisionId, VersionId = review.DraftVersionId, DecidedAt = review.DecidedAt }, ct).ConfigureAwait(false);
        else if (value is BoundaryApproved approval)
        {
            await InsertVersionAsync(
                row with { VersionId = approval.DraftVersionId, EffectiveFrom = approval.EffectiveFrom }, ct).ConfigureAwait(false);
            await InsertDecisionAsync(
                row with { DecisionId = approval.ApprovalDecisionId, VersionId = approval.DraftVersionId, DecidedAt = approval.DecidedAt }, ct).ConfigureAwait(false);
        }
    }

    public async ValueTask<BoundaryView?> GetAsync(Uuid tenantId, Uuid boundaryId, CancellationToken ct = default)
    {
        var row = await ReadIdentityAsync("boundary_current_identities", tenantId, boundaryId.ToString(), ct).ConfigureAwait(false);
        return row is null ? null : (await SourceAsync(tenantId, boundaryId, row, ct).ConfigureAwait(false)).ToView();
    }
    public async ValueTask<Page<BoundaryView>> ListProgramAsync(Uuid tenantId, Uuid programId, int limit, string? cursor,
        CancellationToken ct = default)
    {
        var page = await PageAsync("boundary_current_identities", tenantId, programId.ToString(), limit, cursor,
            ComplianceCoreJsonContext.Default.BoundaryDirectoryIdentity, ct).ConfigureAwait(false);
        var values = new List<BoundaryView>(page.Items.Count);
        foreach (var row in page.Items)
        {
            if (row.Header.ProgramId != programId)
                throw new ImmutableContentIntegrityException("The boundary directory row has another program scope.");
            var source = await SourceAsync(tenantId, row.Header.BoundaryId, row, ct).ConfigureAwait(false);
            if (source.ToView() is { } view)
                values.Add(view);
        }
        return new(values, page.NextCursor);
    }
    public async ValueTask<BoundaryVersionView?> GetVersionAsync(Uuid tenantId, Uuid boundaryId, Uuid versionId,
        CancellationToken ct = default)
    {
        var row = await ReadIdentityAsync("boundary_version_identities", tenantId, ScopedKey(boundaryId, versionId), ct).ConfigureAwait(false);
        return row is null || row.Header.BoundaryId != boundaryId ? null
            : (await SourceAsync(tenantId, boundaryId, row, ct).ConfigureAwait(false)).FindVersion(versionId)
                ?? throw new ImmutableContentUnavailableException("The projected boundary version is not visible at its native source.");
    }
    public async ValueTask<Page<BoundaryVersionView>?> ListVersionsAsync(Uuid tenantId, Uuid boundaryId, int limit,
        string? cursor, CancellationToken ct = default)
    {
        if (await ReadIdentityAsync("boundary_current_identities", tenantId, boundaryId.ToString(), ct).ConfigureAwait(false) is null)
            return null;
        var page = await PageAsync("boundary_version_identities", tenantId, boundaryId.ToString(), limit, cursor,
            ComplianceCoreJsonContext.Default.BoundaryDirectoryIdentity, ct).ConfigureAwait(false);
        var values = new List<BoundaryVersionView>(page.Items.Count);
        BoundaryReadSnapshot? source = null;
        foreach (var row in page.Items)
        {
            source = await SourceAsync(tenantId, boundaryId, row, ct, source).ConfigureAwait(false);
            values.Add(source.FindVersion(row.VersionId!.Value)
                ?? throw new ImmutableContentUnavailableException("The projected boundary version is not visible at its native source."));
        }
        return new(values, page.NextCursor);
    }
    public async ValueTask<BoundaryVersionView?> GetEffectiveVersionAsync(Uuid tenantId, Uuid boundaryId, DateOnly effectiveOn,
        CancellationToken ct = default)
    {
        var row = await ReadIdentityAsync("boundary_current_identities", tenantId, boundaryId.ToString(), ct).ConfigureAwait(false);
        return row is null ? null : (await SourceAsync(tenantId, boundaryId, row, ct).ConfigureAwait(false)).EffectiveVersion(effectiveOn);
    }
    public async ValueTask<BoundaryDecisionView?> GetDecisionAsync(Uuid tenantId, Uuid boundaryId, Uuid decisionId,
        CancellationToken ct = default)
    {
        var row = await ReadIdentityAsync("boundary_decision_identities", tenantId, ScopedKey(boundaryId, decisionId), ct).ConfigureAwait(false);
        return row is null || row.Header.BoundaryId != boundaryId ? null
            : (await SourceAsync(tenantId, boundaryId, row, ct).ConfigureAwait(false)).FindDecision(decisionId)
                ?? throw new ImmutableContentUnavailableException("The projected boundary decision is not visible at its native source.");
    }
    public async ValueTask<Page<BoundaryDecisionView>?> ListDecisionsAsync(Uuid tenantId, Uuid boundaryId, int limit,
        string? cursor, CancellationToken ct = default)
    {
        if (await ReadIdentityAsync("boundary_current_identities", tenantId, boundaryId.ToString(), ct).ConfigureAwait(false) is null)
            return null;
        var page = await PageAsync("boundary_decision_identities", tenantId, boundaryId.ToString(), limit, cursor,
            ComplianceCoreJsonContext.Default.BoundaryDirectoryIdentity, ct).ConfigureAwait(false);
        var values = new List<BoundaryDecisionView>(page.Items.Count);
        BoundaryReadSnapshot? source = null;
        foreach (var row in page.Items)
        {
            source = await SourceAsync(tenantId, boundaryId, row, ct, source).ConfigureAwait(false);
            values.Add(source.FindDecision(row.DecisionId!.Value)
                ?? throw new ImmutableContentUnavailableException("The projected boundary decision is not visible at its native source."));
        }
        return new(values, page.NextCursor);
    }
    ValueTask<BoundaryDirectoryIdentity?> CurrentBoundaryAsync(Uuid id, CancellationToken ct) =>
        CurrentAsync("boundary_current_identities", id.ToString(), ComplianceCoreJsonContext.Default.BoundaryDirectoryIdentity, ct);
    ValueTask<BoundaryDirectoryIdentity?> ReadIdentityAsync(string table, Uuid tenantId, string id, CancellationToken ct) =>
        ReadAsync(table, tenantId, id, ComplianceCoreJsonContext.Default.BoundaryDirectoryIdentity, ct);
    ValueTask InsertIdentityAsync(string table, BoundaryDirectoryIdentity row, CancellationToken ct) =>
        InsertAsync(table, row.Header.TenantId, row.Header.BoundaryId.ToString(), row.Header.ProgramId.ToString(),
            row.Header.BoundaryId.ToString(), row, ComplianceCoreJsonContext.Default.BoundaryDirectoryIdentity, ct);
    ValueTask ReplaceIdentityAsync(BoundaryDirectoryIdentity row, CancellationToken ct) =>
        ReplaceAsync("boundary_current_identities", row.Header.TenantId, row.Header.BoundaryId.ToString(), row.Header.ProgramId.ToString(),
            row.Header.BoundaryId.ToString(), row, ComplianceCoreJsonContext.Default.BoundaryDirectoryIdentity, ct);
    ValueTask InsertVersionAsync(BoundaryDirectoryIdentity row, CancellationToken ct) =>
        InsertAsync("boundary_version_identities", row.Header.TenantId, ScopedKey(row.Header.BoundaryId, row.VersionId!.Value), row.Header.BoundaryId.ToString(),
            row.EffectiveFrom!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), row, ComplianceCoreJsonContext.Default.BoundaryDirectoryIdentity, ct);
    ValueTask InsertDecisionAsync(BoundaryDirectoryIdentity row, CancellationToken ct) =>
        InsertAsync("boundary_decision_identities", row.Header.TenantId, ScopedKey(row.Header.BoundaryId, row.DecisionId!.Value), row.Header.BoundaryId.ToString(),
            row.DecidedAt!.Value.UtcTicks.ToString("D20", CultureInfo.InvariantCulture), row, ComplianceCoreJsonContext.Default.BoundaryDirectoryIdentity, ct);
    static string ScopedKey(Uuid boundaryId, Uuid localId) => $"{boundaryId}:{localId}";
    async ValueTask<BoundaryReadSnapshot> SourceAsync(Uuid tenantId, Uuid boundaryId, BoundaryDirectoryIdentity row,
        CancellationToken ct, BoundaryReadSnapshot? pinned = null)
    {
        if (row.Header.TenantId != tenantId || row.Header.BoundaryId != boundaryId)
            throw new ImmutableContentIntegrityException("The boundary identity has another exact source scope.");
        var source = pinned ?? await boundaries.ReadAsync(tenantId, row.Header.ProgramId, boundaryId, ct).ConfigureAwait(false);
        if (!source.IsCreated || source.ProgramId != row.Header.ProgramId || source.Revision < row.Header.Revision)
            throw new ImmutableContentIntegrityException("The boundary identity differs from its authoritative source.");
        if (row.VersionId is null && row.DecisionId is null && source.IsVisible && source.Owner.Header != row.Header)
            throw new ImmutableContentUnavailableException("The boundary projection has not reached its native current authority.");
        return source;
    }
}
