using System.Text.Json;
using Cntryl.Cassie.Commands;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Readers;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Projections;
using Bdgrz.Compliance.Features.Work.Services;
namespace Bdgrz.Compliance.Features.Work.Readers;

/// <summary>
///     Stores independent closure work in Cassie without deciding the finding workflow. Each relevant committed fact
///     refreshes that finding from authoritative owner streams; Portia owns the atomic SQL-and-checkpoint boundary.
/// </summary>
sealed class CassieFindingClosureWorkItemDirectory(CassieProjectionDatabase database,
    FindingReader findings, TimeProvider clock)
    : CassieProjectionRepository(database, ProjectorName),
        IAccountableWorkItemDirectoryReader, IFindingClosureWorkItemProjection
{
    public const string ProjectorName = "AccountableWorkItemFindingClosureV4";

    string IAccountableWorkItemDirectoryReader.ProjectorName => ProjectorName;
    public IReadOnlyCollection<string> ProjectedKinds => [FindingClosureWork.Kind];

    public EventStreamPattern SourcePattern(Uuid tenantId) => EventStreamPattern.ForPattern(tenantId.ToString());

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(ProjectorName, SourcePattern(tenantId)), ct);

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        if (await FindingWorkScope.OfAsync(domainEvent, findings, ct).ConfigureAwait(false) is not
            var (tenantId, programId, findingId))
            return;
        if (Identity.Pattern != SourcePattern(tenantId))
            throw new InvalidOperationException("Finding closure work must use its bound tenant projection batch.");
        var view = await findings.ReadViewAsync(tenantId, programId, findingId, clock.GetUtcNow(), ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("A finding fact cannot be projected before its raise is visible.");
        await using var command = CreateCommand("""
            INSERT INTO work_finding_closures (projection_key, tenant_id, program_id, finding_id, payload)
            VALUES (@projection, @tenant, @program, @finding, CAST(@payload AS JSONB))
            ON CONFLICT (projection_key, program_id, finding_id) DO UPDATE SET payload = EXCLUDED.payload
            WHERE work_finding_closures.tenant_id = @tenant
            """);
        _ = command.Parameters.Add(new CassieParameter { ParameterName = "projection", Value = CurrentProjectionKey });
        _ = command.Parameters.Add(new CassieParameter { ParameterName = "tenant", Value = tenantId.ToString() });
        _ = command.Parameters.Add(new CassieParameter { ParameterName = "program", Value = programId.ToString() });
        _ = command.Parameters.Add(new CassieParameter { ParameterName = "finding", Value = findingId.ToString() });
        _ = command.Parameters.Add(new CassieParameter { ParameterName = "payload",
            Value = JsonSerializer.Serialize(Project(view), ComplianceCoreJsonContext.Default.FindingClosureWorkState) });
        if (await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) != 1)
            throw new InvalidOperationException("Finding closure work cannot change its tenant scope.");
    }

    public ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId, Uuid programId,
        CancellationToken ct = default) => LoadProgramAsync(tenantId, programId, clock.GetUtcNow(), DateOnly.MaxValue, null, ct);

    public async ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId, Uuid programId,
        DateTimeOffset now, DateOnly horizon, Uuid? workItemId, CancellationToken ct = default)
    {
        var source = await findings.ReadAsync(tenantId, programId, now, ct).ConfigureAwait(false);
        if (!source.IsSuccess)
            return Result<IReadOnlyList<WorkCandidate>>.Failure(source.Error);
        var expected = source.Value.Findings.Select(FindingClosureWork.Candidate).OfType<WorkCandidate>().ToArray();
        var projected = await LoadProjectedProgramAsync(tenantId, programId, ct).ConfigureAwait(false);
        if (!projected.IsSuccess)
            return projected;
        return SameCandidates(expected, projected.Value) ? projected :
            Result<IReadOnlyList<WorkCandidate>>.Failure(new RequestError(RequestErrorKind.Conflict,
                "Finding work has not reached its current source, or the source changed. Retry.", isTransient: true));
    }

    public async ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProjectedProgramAsync(Uuid tenantId, Uuid programId,
        CancellationToken ct = default)
    {
        var candidates = new List<WorkCandidate>();
        await using var read = await OpenReadAsync(ct).ConfigureAwait(false);
        await using var command = read.CreateCommand("""
            SELECT finding_id, CAST(payload AS TEXT) FROM work_finding_closures
            WHERE projection_key = @projection AND tenant_id = @tenant AND program_id = @program
            ORDER BY finding_id
            """);
        _ = command.Parameters.Add(new CassieParameter { ParameterName = "projection", Value = QueryKey(tenantId) });
        _ = command.Parameters.Add(new CassieParameter { ParameterName = "tenant", Value = tenantId.ToString() });
        _ = command.Parameters.Add(new CassieParameter { ParameterName = "program", Value = programId.ToString() });
        await using var rows = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rows.ReadAsync(ct).ConfigureAwait(false))
        {
            FindingClosureWorkState? finding;
            try
            { finding = JsonSerializer.Deserialize(rows.GetString(1), ComplianceCoreJsonContext.Default.FindingClosureWorkState); }
            catch (JsonException)
            { return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope()); }
            if (finding is null || finding.TenantId != tenantId || finding.ProgramId != programId ||
                finding.FindingId == Uuid.Empty || finding.FindingId.ToString() != rows.GetString(0) ||
                finding.OwnerMemberId == Uuid.Empty || finding.Revision < 1 || finding.Actions is null ||
                finding.Actions.Any(static action => action is null || action.OwnerMemberId == Uuid.Empty ||
                    action.ActionId == Uuid.Empty || action.Status is not (FindingStates.Open or FindingStates.Completed or FindingStates.Cancelled) ||
                    action.Status == FindingStates.Completed && (action.CompletedByMemberId is null || action.CompletedByMemberId == Uuid.Empty) ||
                    action.Status == FindingStates.Cancelled && (action.CancelledByMemberId is null || action.CancelledByMemberId == Uuid.Empty ||
                        string.IsNullOrWhiteSpace(action.CancellationReason) || action.CancellationReason.Length > 4000 ||
                        action.CompletedByMemberId is not null)))
                return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope());
            if (FindingClosureWork.Candidate(finding) is { } candidate)
                candidates.Add(candidate);
        }
        return Result<IReadOnlyList<WorkCandidate>>.Success(candidates);
    }

    string QueryKey(Uuid tenantId) => GetProjectionKey(new CheckpointIdentity(ProjectorName, SourcePattern(tenantId)));

    static RequestError InvalidScope() => new(RequestErrorKind.Conflict,
        "The finding closure work projection has invalid source scope.");

    static FindingClosureWorkState Project(FindingView view) => new(view.TenantId, view.ProgramId, view.FindingId,
        view.Revision, view.Title, view.Severity, view.OwnerMemberId, view.DueOn,
        view.CorrectiveActions.Select(static action => new FindingClosureActionState(action.ActionId,
            action.OwnerMemberId, action.Status, action.CompletedByMemberId, action.CancelledByMemberId,
            action.CancellationReason)).ToArray(), view.Closure is not null, view.History.Max(static entry => entry.At));

    static bool SameCandidates(WorkCandidate[] expected, IReadOnlyList<WorkCandidate> actual) =>
        expected.Length == actual.Count && expected.OrderBy(static item => item.WorkItemId.ToString(), StringComparer.Ordinal)
            .Zip(actual.OrderBy(static item => item.WorkItemId.ToString(), StringComparer.Ordinal))
            .All(static pair => pair.First.Excluded.SetEquals(pair.Second.Excluded) &&
                pair.First with { ProgramId = pair.Second.ProgramId, Excluded = pair.Second.Excluded } == pair.Second);
}
