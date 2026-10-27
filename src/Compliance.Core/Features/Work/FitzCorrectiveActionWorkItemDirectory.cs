using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Readers;
using Bdgrz.Compliance.Features.Work.Projections;
using Bdgrz.Compliance.Features.Work.Readers;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>
///     Projects the open corrective actions of unclosed findings as accountable work. Every fact of the finding owner or
///     one of its corrective action owners replaces the work items of exactly that finding from the authoritative
///     owners, so the projection can neither skip nor reorder a fact and never folds a legacy ledger. An admitted action
///     is work as soon as the finding accepts it; completing it waits for its materialization.
/// </summary>
sealed class FitzCorrectiveActionWorkItemDirectory(IKvClient client,
    FindingReader findings, TimeProvider clock)
    : FitzKvProjectionStore(client,
        "kv://bdgrz/accountable-work-items/corrective-actions-v4", ProjectorName),
      IAccountableWorkItemDirectoryReader, ICorrectiveActionWorkItemProjection
{
    public const string ProjectorName = "AccountableWorkItemCorrectiveActionV4";
    const string RevisionKey = ProjectorName;

    string IAccountableWorkItemDirectoryReader.ProjectorName => ProjectorName;

    public IReadOnlyCollection<string> ProjectedKinds => [WorkSource.CorrectiveAction];

    public EventStreamPattern SourcePattern(Uuid tenantId) =>
        EventStreamPattern.ForPattern(tenantId.ToString());

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) => base.LoadCheckpointAsync(new CheckpointIdentity(
        ProjectorName, SourcePattern(tenantId)), ct);

    public async ValueTask<long> LoadRevisionAsync(Uuid tenantId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return (await CorrectiveActionWorkItemDirectorySchema.Revisions.GetAsync(tx,
            RevisionKey, ct).ConfigureAwait(false))?.Revision ?? 0;
    }

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        if (await FindingWorkScope.OfAsync(domainEvent, findings, ct).ConfigureAwait(false) is not
            var (tenantId, programId, findingId))
            return;
        var view = await findings.ReadViewAsync(tenantId, programId, findingId, clock.GetUtcNow(), ct)
            .ConfigureAwait(false);
        if (view is null)
            throw new InvalidOperationException("A finding fact cannot be projected before its raise is visible.");

        var wanted = Candidates(view).Select(candidate => AccountableWorkItemView.FromCandidate(tenantId, programId,
            candidate)).ToDictionary(static item => item.WorkItemId);
        string? cursor = null;
        var existing = new List<AccountableWorkItemView>();
        do
        {
            var page = await CorrectiveActionWorkItemDirectorySchema.WorkItems.QueryAsync(Transaction,
                CorrectiveActionWorkItemDirectorySchema.ByFinding.Query()
                    .WithPrefix(programId.ToString(), findingId.ToString()).Take(200).After(cursor), ct)
                .ConfigureAwait(false);
            existing.AddRange(page.Items);
            cursor = page.NextCursor;
        } while (cursor is not null);

        foreach (var item in existing)
        {
            ValidateWorkItem(item, tenantId, programId, findingId);
            if (!wanted.TryGetValue(item.WorkItemId, out var replacement))
                await CorrectiveActionWorkItemDirectorySchema.WorkItems.DeleteAsync(Transaction, item, ct)
                    .ConfigureAwait(false);
            else if (replacement != item)
                await CorrectiveActionWorkItemDirectorySchema.WorkItems.ReplaceAsync(Transaction, item, replacement, ct)
                    .ConfigureAwait(false);
            wanted.Remove(item.WorkItemId);
        }

        foreach (var item in wanted.Values)
            await CorrectiveActionWorkItemDirectorySchema.WorkItems.InsertAsync(Transaction, item, ct)
                .ConfigureAwait(false);
        await IncrementRevisionAsync(ct).ConfigureAwait(false);
    }

    public ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
        Uuid programId, CancellationToken ct = default) =>
        LoadProgramAsync(tenantId, programId, clock.GetUtcNow(), DateOnly.MaxValue, null, ct);

    public async ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
        Uuid programId, DateTimeOffset now, DateOnly horizon, Uuid? workItemId,
        CancellationToken ct = default)
    {
        var source = await findings.ReadAsync(tenantId, programId, now, ct).ConfigureAwait(false);
        if (!source.IsSuccess)
            return Result<IReadOnlyList<WorkCandidate>>.Failure(source.Error);
        var expected = source.Value.Findings.SelectMany(Candidates).ToArray();
        var projected = await LoadProjectedProgramAsync(tenantId, programId, ct).ConfigureAwait(false);
        if (!projected.IsSuccess)
            return projected;
        if (!SameCandidates(expected, projected.Value))
            return Result<IReadOnlyList<WorkCandidate>>.Failure(new RequestError(RequestErrorKind.Conflict,
                "Finding work has not reached its current source, or the source changed. Retry.", isTransient: true));
        return projected;
    }

    public async ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProjectedProgramAsync(Uuid tenantId,
        Uuid programId, CancellationToken ct = default)
    {
        var candidates = new List<WorkCandidate>();
        string? cursor = null;
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        do
        {
            var page = await CorrectiveActionWorkItemDirectorySchema.WorkItems.QueryAsync(tx,
                CorrectiveActionWorkItemDirectorySchema.ByProgram.Query()
                    .WithPrefix(programId.ToString()).Take(200).After(cursor), ct)
                .ConfigureAwait(false);
            foreach (var item in page.Items)
            {
                if (item.TenantId != tenantId || item.ProgramId != programId ||
                    item.Kind != WorkSource.CorrectiveAction || item.FindingId is null ||
                    item.WorkItemId != WorkCandidate.IdFor(item.SourceId, WorkSource.CorrectiveAction))
                    return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope());
                candidates.Add(item.ToCandidate());
            }
            cursor = page.NextCursor;
        } while (cursor is not null);

        return Result<IReadOnlyList<WorkCandidate>>.Success(candidates);
    }

    async ValueTask IncrementRevisionAsync(CancellationToken ct)
    {
        var current = await CorrectiveActionWorkItemDirectorySchema.Revisions.GetAsync(
            Transaction, RevisionKey, ct).ConfigureAwait(false);
        var next = new AccountableWorkItemProjectionRevision(ProjectorName,
            checked((current?.Revision ?? 0) + 1));
        if (current is null)
            await CorrectiveActionWorkItemDirectorySchema.Revisions.InsertAsync(Transaction, next, ct)
                .ConfigureAwait(false);
        else
            await CorrectiveActionWorkItemDirectorySchema.Revisions.ReplaceAsync(Transaction,
                current, next, ct).ConfigureAwait(false);
    }

    static void ValidateWorkItem(AccountableWorkItemView item, Uuid tenantId, Uuid programId, Uuid findingId)
    {
        if (item.TenantId != tenantId || item.ProgramId != programId || item.FindingId != findingId ||
            item.Kind != WorkSource.CorrectiveAction ||
            item.WorkItemId != WorkCandidate.IdFor(item.SourceId, WorkSource.CorrectiveAction))
            throw new InvalidOperationException(
                "A corrective action work item cannot change tenant, program, or source scope.");
    }

    static RequestError InvalidScope() => new(RequestErrorKind.Conflict,
        "The corrective action work projection has an invalid tenant, program, or source scope.");

    static WorkCandidate[] Candidates(FindingView finding) => finding.Status == FindingStates.Closed
        ? []
        : finding.CorrectiveActions.Where(static action => action.Status == FindingStates.Open)
            .Select(action => new WorkCandidate(WorkCandidate.IdFor(action.ActionId, WorkSource.CorrectiveAction),
                WorkSource.CorrectiveAction, action.ActionId, null, finding.FindingId, action.Description,
                $"Corrective action for finding \"{finding.Title}\".", action.DueOn,
                FindingStates.Materiality(finding.Severity), "complete",
                $"/api/v1/tenants/{finding.TenantId}/programs/{finding.ProgramId}/findings/{finding.FindingId}/corrective-actions/{action.ActionId}/completions",
                new OperatingHolder(OperatingAuthority.MemberHolder, action.OwnerMemberId), null, new HashSet<Uuid>(),
                action.AddedAt)).ToArray();

    static bool SameCandidates(WorkCandidate[] expected, IReadOnlyList<WorkCandidate> actual) =>
        expected.Length == actual.Count && expected.OrderBy(static item => item.WorkItemId.ToString(), StringComparer.Ordinal)
            .Zip(actual.OrderBy(static item => item.WorkItemId.ToString(), StringComparer.Ordinal))
            .All(static pair => pair.First.Excluded.SetEquals(pair.Second.Excluded) &&
                pair.First with { ProgramId = pair.Second.ProgramId, Excluded = pair.Second.Excluded } == pair.Second);
}
