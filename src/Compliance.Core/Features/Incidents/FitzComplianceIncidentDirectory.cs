using System.Text.Json;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

sealed class FitzComplianceIncidentDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/compliance-incident-directory/projection-v1",
        ComplianceIncidentDirectoryProjector.ProjectorName),
      IComplianceIncidentDirectoryReader, IComplianceIncidentDirectoryProjection
{
    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        (Uuid TenantId, Uuid SubjectId, long Revision, Uuid? ActorMemberId,
            DateTimeOffset OccurredAt, ComplianceIncidentView? State) envelope = domainEvent switch
            {
                ComplianceIncidentRecorded ev => (ev.TenantId, ev.SubjectId, ev.Revision,
                    ev.ActorMemberId, ev.OccurredAt, ev.State),
                ComplianceIncidentRevised ev => (ev.TenantId, ev.SubjectId, ev.Revision,
                    ev.ActorMemberId, ev.OccurredAt, ev.State),
                ComplianceIncidentImpactAssessed ev => (ev.TenantId, ev.SubjectId, ev.Revision,
                    ev.ActorMemberId, ev.OccurredAt, ev.State),
                ComplianceIncidentSystemDescriptionDecisionRecorded ev => (ev.TenantId,
                    ev.SubjectId, ev.Revision, ev.ActorMemberId, ev.OccurredAt, ev.State),
                IncidentResolutionRecorded ev => (ev.TenantId, ev.SubjectId, ev.Revision,
                    ev.ActorMemberId, ev.OccurredAt, ev.State),
                _ => (Uuid.Empty, Uuid.Empty, 0L, (Uuid?)null, default(DateTimeOffset), null),
            };
        var (tenantId, subjectId, revision, actorMemberId, occurredAt, state) = envelope;
        if (state is null)
            return;
        var actorMismatch = actorMemberId is { } actor && actor != state.ActorMemberId;
        if (tenantId == Uuid.Empty || subjectId == Uuid.Empty || revision <= 0 ||
            occurredAt == default || state.TenantId != tenantId || state.ProgramId == Uuid.Empty ||
            state.ComplianceIncidentId != subjectId || state.Revision != revision ||
            state.UpdatedAt != occurredAt || actorMismatch || state.ActorMemberId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(state.ActorDisplay) || state.Current is null ||
            state.Versions is null || state.Versions.Count is < 1 or > 32 ||
            state.CurrentVersionNumber is < 1 or > 32 ||
            state.Versions.Count != state.CurrentVersionNumber ||
            state.Versions[^1].Version != state.CurrentVersionNumber ||
            state.Versions[^1].ContentSha256 != state.Current.ContentSha256 ||
            state.Assessments is null || state.Assessments.Count > state.Versions.Count ||
            state.Assessments.Select(static assessment => assessment.ContentVersion).Distinct().Count() !=
                state.Assessments.Count || state.SystemDescriptionDecisions is null ||
            state.SystemDescriptionDecisions.Count > state.Assessments.Count)
            throw new InvalidOperationException("The compliance-incident projection event is invalid.");

        var summary = ToSummary(state);
        var current = await ComplianceIncidentDirectorySchema.Details.GetAsync(Transaction,
            subjectId, ct).ConfigureAwait(false);
        if (current is null)
        {
            if (domainEvent is not ComplianceIncidentRecorded || revision != 1 ||
                state.Versions.Count != 1 || state.CurrentVersionNumber != 1)
                throw new InvalidOperationException(
                    "A compliance incident must be recorded before later revisions are projected.");
            await ComplianceIncidentDirectorySchema.Details.InsertAsync(Transaction, state, ct)
                .ConfigureAwait(false);
            await ComplianceIncidentDirectorySchema.Summaries.InsertAsync(Transaction, summary, ct)
                .ConfigureAwait(false);
            return;
        }
        if (current.TenantId != tenantId || current.ProgramId != state.ProgramId ||
            current.ComplianceIncidentId != subjectId)
            throw new InvalidOperationException(
                "A compliance-incident identity changed during projection.");
        if (revision < current.Revision)
            return;
        if (revision == current.Revision)
        {
            if (!SameSnapshot(current, state))
                throw new InvalidOperationException(
                    "A compliance-incident revision has conflicting projection content.");
            var existingSummary = await ComplianceIncidentDirectorySchema.Summaries.GetAsync(
                Transaction, subjectId, ct).ConfigureAwait(false);
            if (existingSummary != summary)
                throw new InvalidOperationException(
                    "A compliance-incident summary has conflicting projection content.");
            return;
        }
        if (revision != current.Revision + 1)
            throw new InvalidOperationException("The compliance-incident projection skipped a revision.");

        var currentSummary = await ComplianceIncidentDirectorySchema.Summaries.GetAsync(Transaction,
            subjectId, ct).ConfigureAwait(false) ?? throw new InvalidOperationException(
            "A compliance-incident summary is missing from its projection.");
        await ComplianceIncidentDirectorySchema.Details.ReplaceAsync(Transaction, current, state, ct)
            .ConfigureAwait(false);
        await ComplianceIncidentDirectorySchema.Summaries.ReplaceAsync(Transaction, currentSummary,
            summary, ct).ConfigureAwait(false);
    }

    public async ValueTask<ComplianceIncidentView?> GetAsync(Uuid tenantId, Uuid incidentId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ComplianceIncidentDirectorySchema.Details.GetAsync(tx, incidentId, ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<Page<ComplianceIncidentSummaryView>> ListAsync(Uuid tenantId,
        Uuid programId, int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ComplianceIncidentDirectorySchema.Summaries.QueryAsync(tx,
            ComplianceIncidentDirectorySchema.ByProgramUpdatedAt.Query()
                .WithPrefix(programId.ToString()).Take(Math.Clamp(limit, 1, 200)).After(cursor), ct)
            .ConfigureAwait(false);
    }

    static ComplianceIncidentSummaryView ToSummary(ComplianceIncidentView state)
    {
        var current = state.Current ?? throw new InvalidOperationException(
            "A compliance-incident projection requires current content.");
        return new ComplianceIncidentSummaryView(state.TenantId, state.ProgramId,
            state.ComplianceIncidentId, current.Content.Title, state.Status, state.UpdatedAt);
    }

    static bool SameSnapshot(ComplianceIncidentView first, ComplianceIncidentView second)
    {
        var firstJson = JsonSerializer.SerializeToUtf8Bytes(first,
            ComplianceCoreJsonContext.Default.ComplianceIncidentView);
        var secondJson = JsonSerializer.SerializeToUtf8Bytes(second,
            ComplianceCoreJsonContext.Default.ComplianceIncidentView);
        return firstJson.AsSpan().SequenceEqual(secondJson);
    }
}
