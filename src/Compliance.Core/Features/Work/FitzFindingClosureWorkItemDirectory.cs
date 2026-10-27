using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Readers;
using Bdgrz.Compliance.Features.Work.Projections;
using Bdgrz.Compliance.Features.Work.Readers;
using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>
///     Projects independent closure work without changing the finding workflow. Every fact of the finding owner or one
///     of its corrective action owners refreshes the stored state of exactly that finding from the authoritative owners,
///     so the projection can neither skip nor reorder a fact and never folds a legacy ledger.
/// </summary>
sealed class FitzFindingClosureWorkItemDirectory(IKvClient client,
    FindingReader findings, TimeProvider clock)
    : FitzKvProjectionStore(client, "kv://bdgrz/accountable-work-items/finding-closure-v4",
        ProjectorName), IAccountableWorkItemDirectoryReader, IFindingClosureWorkItemProjection
{
    public const string ProjectorName = "AccountableWorkItemFindingClosureV4";

    string IAccountableWorkItemDirectoryReader.ProjectorName => ProjectorName;
    public IReadOnlyCollection<string> ProjectedKinds => [FindingClosureWork.Kind];

    public EventStreamPattern SourcePattern(Uuid tenantId) =>
        EventStreamPattern.ForPattern(tenantId.ToString());

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) => base.LoadCheckpointAsync(new CheckpointIdentity(
        ProjectorName, SourcePattern(tenantId)), ct);

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        if (await FindingWorkScope.OfAsync(domainEvent, findings, ct).ConfigureAwait(false) is not
            var (tenantId, programId, findingId))
            return;
        var view = await findings.ReadViewAsync(tenantId, programId, findingId, clock.GetUtcNow(), ct)
            .ConfigureAwait(false);
        if (view is null)
            throw new InvalidOperationException("A finding fact cannot be projected before its raise is visible.");
        var next = Project(view);
        var key = FindingAggregate.StreamId(programId, findingId);
        var current = await FindingClosureWorkItemDirectorySchema.Findings.GetAsync(Transaction, key, ct)
            .ConfigureAwait(false);
        if (current is null)
            await FindingClosureWorkItemDirectorySchema.Findings.InsertAsync(Transaction, next, ct)
                .ConfigureAwait(false);
        else if (current.TenantId != tenantId || current.ProgramId != programId)
            throw new InvalidOperationException("Finding closure work cannot change scope.");
        else
            await FindingClosureWorkItemDirectorySchema.Findings.ReplaceAsync(Transaction, current, next, ct)
                .ConfigureAwait(false);
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
        var expected = source.Value.Findings.Select(FindingClosureWork.Candidate).OfType<WorkCandidate>().ToArray();
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
            var page = await FindingClosureWorkItemDirectorySchema.Findings.QueryAsync(tx,
                FindingClosureWorkItemDirectorySchema.ByProgram.Query()
                    .WithPrefix(programId.ToString()).Take(200).After(cursor), ct)
                .ConfigureAwait(false);
            foreach (var finding in page.Items)
            {
                if (finding.TenantId != tenantId || finding.ProgramId != programId ||
                    finding.FindingId == Uuid.Empty || finding.OwnerMemberId == Uuid.Empty ||
                    finding.Revision < 1 || finding.Actions is null || finding.Actions.Any(static action =>
                        action is null || action.OwnerMemberId == Uuid.Empty || action.ActionId == Uuid.Empty ||
                        action.Status is not (FindingStates.Open or FindingStates.Completed) ||
                        action.Status == FindingStates.Completed &&
                        (action.CompletedByMemberId is null || action.CompletedByMemberId == Uuid.Empty)))
                    return Result<IReadOnlyList<WorkCandidate>>.Failure(new RequestError(
                        RequestErrorKind.Conflict, "The finding closure work projection has invalid source scope."));
                if (FindingClosureWork.Candidate(finding) is { } candidate)
                    candidates.Add(candidate);
            }
            cursor = page.NextCursor;
        } while (cursor is not null);
        return Result<IReadOnlyList<WorkCandidate>>.Success(candidates);
    }

    static FindingClosureWorkState Project(FindingView view) => new(view.TenantId, view.ProgramId, view.FindingId,
        view.Revision, view.Title, view.Severity, view.OwnerMemberId, view.DueOn,
        view.CorrectiveActions.Select(static action => new FindingClosureActionState(action.ActionId,
            action.OwnerMemberId, action.Status, action.CompletedByMemberId)).ToArray(),
        view.Closure is not null, view.History.Max(static entry => entry.At));

    static bool SameCandidates(WorkCandidate[] expected, IReadOnlyList<WorkCandidate> actual) =>
        expected.Length == actual.Count && expected.OrderBy(static item => item.WorkItemId.ToString(), StringComparer.Ordinal)
            .Zip(actual.OrderBy(static item => item.WorkItemId.ToString(), StringComparer.Ordinal))
            .All(static pair => pair.First.Excluded.SetEquals(pair.Second.Excluded) &&
                pair.First with { ProgramId = pair.Second.ProgramId, Excluded = pair.Second.Excluded } == pair.Second);
}
