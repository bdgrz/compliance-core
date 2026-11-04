using System.Globalization;
using System.Text.Json;
using Cntryl.Cassie.Commands;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Readers;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Projections;
using Bdgrz.Compliance.Features.Work.Services;
namespace Bdgrz.Compliance.Features.Work.Readers;

/// <summary>
///     Stores derived corrective-action work in Cassie. The authoritative finding and action streams still supply the
///     business state; Portia commits these SQL changes and the consumed checkpoint in its own projection transaction.
/// </summary>
sealed class CassieCorrectiveActionWorkItemDirectory(CassieProjectionDatabase database,
    FindingReader findings, TimeProvider clock)
    : CassieProjectionRepository(database, ProjectorName),
        IAccountableWorkItemDirectoryReader, ICorrectiveActionWorkItemProjection
{
    public const string ProjectorName = "AccountableWorkItemCorrectiveActionV4";

    string IAccountableWorkItemDirectoryReader.ProjectorName => ProjectorName;
    public IReadOnlyCollection<string> ProjectedKinds => [WorkSource.CorrectiveAction];

    public EventStreamPattern SourcePattern(Uuid tenantId) => EventStreamPattern.ForPattern(tenantId.ToString());

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(ProjectorName, SourcePattern(tenantId)), ct);

    public async ValueTask<long> LoadRevisionAsync(Uuid tenantId, CancellationToken ct = default)
    {
        await using var read = await OpenReadAsync(ct).ConfigureAwait(false);
        await using var command = read.CreateCommand("""
            SELECT revision FROM work_corrective_action_revisions
            WHERE projection_key = @projection AND tenant_id = @tenant
            """);
        _ = command.Parameters.Add(new CassieParameter { ParameterName = "projection", Value = QueryKey(tenantId) });
        _ = command.Parameters.Add(new CassieParameter { ParameterName = "tenant", Value = tenantId.ToString() });
        var value = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return value is null or DBNull ? 0 : Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        if (await FindingWorkScope.OfAsync(domainEvent, findings, ct).ConfigureAwait(false) is not
            var (tenantId, programId, findingId))
            return;
        if (Identity.Pattern != SourcePattern(tenantId))
            throw new InvalidOperationException("Corrective-action work must use its bound tenant projection batch.");
        var view = await findings.ReadViewAsync(tenantId, programId, findingId, clock.GetUtcNow(), ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("A finding fact cannot be projected before its raise is visible.");
        await ValidatePreviousRowsAsync(tenantId, programId, findingId, ct).ConfigureAwait(false);
        await using (var delete = CreateCommand("""
            DELETE FROM work_corrective_actions
            WHERE projection_key = @projection AND tenant_id = @tenant AND program_id = @program AND finding_id = @finding
            """))
        {
            Scope(delete, tenantId, programId, findingId);
            _ = await delete.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        foreach (var candidate in Candidates(view))
        {
            var item = AccountableWorkItemView.FromCandidate(tenantId, programId, candidate);
            await using var insert = CreateCommand("""
                INSERT INTO work_corrective_actions (projection_key, tenant_id, program_id, finding_id, work_item_id, payload)
                VALUES (@projection, @tenant, @program, @finding, @work_item, CAST(@payload AS JSONB))
                """);
            Scope(insert, tenantId, programId, findingId);
            _ = insert.Parameters.Add(new CassieParameter { ParameterName = "work_item", Value = item.WorkItemId.ToString() });
            _ = insert.Parameters.Add(new CassieParameter { ParameterName = "payload",
                Value = JsonSerializer.Serialize(item, ComplianceCoreJsonContext.Default.AccountableWorkItemView) });
            _ = await insert.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        await using var revision = CreateCommand("""
            INSERT INTO work_corrective_action_revisions (projection_key, tenant_id, revision) VALUES (@projection, @tenant, 1)
            ON CONFLICT (projection_key) DO UPDATE SET revision = work_corrective_action_revisions.revision + 1
            WHERE work_corrective_action_revisions.tenant_id = @tenant
            """);
        _ = revision.Parameters.Add(new CassieParameter { ParameterName = "projection", Value = CurrentProjectionKey });
        _ = revision.Parameters.Add(new CassieParameter { ParameterName = "tenant", Value = tenantId.ToString() });
        if (await revision.ExecuteNonQueryAsync(ct).ConfigureAwait(false) != 1)
            throw new InvalidOperationException("Corrective-action work cannot change its tenant scope.");
    }

    public ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId, Uuid programId,
        CancellationToken ct = default) => LoadProgramAsync(tenantId, programId, clock.GetUtcNow(), DateOnly.MaxValue, null, ct);

    public async ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId, Uuid programId,
        DateTimeOffset now, DateOnly horizon, Uuid? workItemId, CancellationToken ct = default)
    {
        var source = await findings.ReadAsync(tenantId, programId, now, ct).ConfigureAwait(false);
        if (!source.IsSuccess)
            return Result<IReadOnlyList<WorkCandidate>>.Failure(source.Error);
        var expected = source.Value.Findings.SelectMany(Candidates).ToArray();
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
            SELECT finding_id, work_item_id, CAST(payload AS TEXT) FROM work_corrective_actions
            WHERE projection_key = @projection AND tenant_id = @tenant AND program_id = @program
            ORDER BY work_item_id
            """);
        _ = command.Parameters.Add(new CassieParameter { ParameterName = "projection", Value = QueryKey(tenantId) });
        _ = command.Parameters.Add(new CassieParameter { ParameterName = "tenant", Value = tenantId.ToString() });
        _ = command.Parameters.Add(new CassieParameter { ParameterName = "program", Value = programId.ToString() });
        await using var rows = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rows.ReadAsync(ct).ConfigureAwait(false))
        {
            AccountableWorkItemView? item;
            try
            { item = JsonSerializer.Deserialize(rows.GetString(2), ComplianceCoreJsonContext.Default.AccountableWorkItemView); }
            catch (JsonException)
            { return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope()); }
            if (item is null || item.TenantId != tenantId || item.ProgramId != programId ||
                item.FindingId is null || item.FindingId == Uuid.Empty || item.FindingId.Value.ToString() != rows.GetString(0) ||
                item.WorkItemId.ToString() != rows.GetString(1) || item.Kind != WorkSource.CorrectiveAction ||
                item.SourceId == Uuid.Empty || item.WorkItemId != WorkCandidate.IdFor(item.SourceId, WorkSource.CorrectiveAction))
                return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope());
            candidates.Add(item.ToCandidate());
        }
        return Result<IReadOnlyList<WorkCandidate>>.Success(candidates);
    }

    string QueryKey(Uuid tenantId) => GetProjectionKey(new CheckpointIdentity(ProjectorName, SourcePattern(tenantId)));

    async ValueTask ValidatePreviousRowsAsync(Uuid tenantId, Uuid programId, Uuid findingId, CancellationToken ct)
    {
        await using var command = CreateCommand("""
            SELECT CAST(payload AS TEXT) FROM work_corrective_actions
            WHERE projection_key = @projection AND tenant_id = @tenant AND program_id = @program AND finding_id = @finding
            """);
        Scope(command, tenantId, programId, findingId);
        await using var rows = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rows.ReadAsync(ct).ConfigureAwait(false))
        {
            var item = JsonSerializer.Deserialize(rows.GetString(0), ComplianceCoreJsonContext.Default.AccountableWorkItemView);
            if (item is null || item.TenantId != tenantId || item.ProgramId != programId || item.FindingId != findingId ||
                item.Kind != WorkSource.CorrectiveAction || item.WorkItemId != WorkCandidate.IdFor(item.SourceId, WorkSource.CorrectiveAction))
                throw new InvalidOperationException("A corrective action work item cannot change tenant, program, or source scope.");
        }
    }

    void Scope(CassieCommand command, Uuid tenantId, Uuid programId, Uuid findingId)
    {
        _ = command.Parameters.Add(new CassieParameter { ParameterName = "projection", Value = CurrentProjectionKey });
        _ = command.Parameters.Add(new CassieParameter { ParameterName = "tenant", Value = tenantId.ToString() });
        _ = command.Parameters.Add(new CassieParameter { ParameterName = "program", Value = programId.ToString() });
        _ = command.Parameters.Add(new CassieParameter { ParameterName = "finding", Value = findingId.ToString() });
    }

    static RequestError InvalidScope() => new(RequestErrorKind.Conflict,
        "The corrective action work projection has an invalid tenant, program, or source scope.");

    static WorkCandidate[] Candidates(FindingView finding) => finding.Status == FindingStates.Closed ? [] :
        finding.CorrectiveActions.Where(static action => action.Status == FindingStates.Open)
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
