using Bdgrz.Compliance.Features.AuditPopulations;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Resolves one complete exact frozen snapshot population inside the accepting operation's preparation.</summary>
public sealed class SystemDescriptionSourceResolver(IAggregateReader reader, TypeIIPeriodReader periods, SystemDescriptionSnapshotSources proofs)
{
    public async ValueTask<Result<SystemDescriptionResolvedSources>> ResolveAsync(Uuid tenant, Uuid program, Uuid examination,
        SystemDescriptionScope? selected, CancellationToken ct)
    {
        if (selected is null || selected.Stage == "type_i")
        {
            var engagement = await reader.HydrateAsync(new ExaminationEngagement(tenant, program, examination), ct).ConfigureAwait(false);
            var baseline = engagement.Baseline;
            if (engagement.Current is not { Status: "baseline_frozen" } || baseline is null || baseline.Status is not ("frozen" or "amended") ||
                baseline.TenantId != tenant || baseline.ProgramId != program || baseline.ExaminationEngagementId != examination ||
                selected is not null && baseline.TypeIBaselineId != selected.TypeIBaselineId)
                return Fail("The description requires the examination's current exact frozen Type I baseline.");
            var scope = selected ?? new SystemDescriptionScope("type_i", examination, baseline.TypeIBaselineId, null, null);
            var sources = baseline.Sources.Select(item => new SystemDescriptionSourceReference(item.Kind, item.RecordId, item.VersionId,
                item.Revision, item.ContentSha256, "current")).ToArray();
            var proof = await proofs.CaptureAsync(tenant, program, scope, sources, engagement.Stream, value => value switch
            {
                TypeIBaselineFrozen item => item.Baseline.TypeIBaselineId == baseline.TypeIBaselineId && item.Baseline.Revision == baseline.Revision && item.Baseline.ContentSha256 == baseline.ContentSha256,
                TypeIBaselineAmended item => item.Baseline.TypeIBaselineId == baseline.TypeIBaselineId && item.Baseline.Revision == baseline.Revision && item.Baseline.ContentSha256 == baseline.ContentSha256,
                _ => false,
            }, ct).ConfigureAwait(false);
            return Result<SystemDescriptionResolvedSources>.Success(new(scope, sources, proof));
        }
        if (selected.Stage != "type_ii" || selected.TypeIIPeriodId is not { } periodId || selected.TypeIBaselineId is not { } baselineId ||
            selected.CloseSnapshotId is not { } closeId)
            return Fail("The description requires an exact Type I baseline or Type II frozen close scope.");
        var period = await periods.ReadAsync(tenant, program, periodId, ct).ConfigureAwait(false);
        if (period.View is not { Status: "closed" } current || current.ExaminationId != examination ||
            current.CloseSnapshots.Count == 0 || current.CloseSnapshots[^1].CloseSnapshotId != closeId)
            return Fail("The description must bind the current exact frozen Type II close snapshot.");
        var plan = await reader.HydrateAsync(new TypeIIPlan(tenant, program, current.TypeIIPlanId), ct).ConfigureAwait(false);
        if (plan.Current is not { Status: "approved", Current: { } version } state || state.CurrentVersionNumber != current.TypeIIPlanVersion ||
            version.Content.ExaminationId != examination || version.Content.TypeIBaselineId != baselineId)
            return Fail("The description's Type II close is not backed by its exact approved plan and Type I baseline.");
        var selectedSources = current.CloseSnapshots[^1].SourceReferences.Select(item => new SystemDescriptionSourceReference(item.SourceType,
            item.RecordId, item.VersionId, item.Version, item.ContentSha256, "current")).ToArray();
        var reference = await proofs.CaptureAsync(tenant, program, selected, selectedSources, period.Stream, value => value is ITypeIIPeriodStateFact fact &&
            (value is ObservationPeriodCloseFrozen or TypeIIPeriodCloseAmended) && fact.Header.LatestCloseSnapshotId == closeId, ct,
            (plan.Stream, value => value is TypeIIPlanApproved approved && approved.State.CurrentVersionNumber == current.TypeIIPlanVersion &&
                approved.State.Current?.ContentSha256 == current.TypeIIPlanContentSha256)).ConfigureAwait(false);
        return Result<SystemDescriptionResolvedSources>.Success(new(selected, selectedSources, reference));
    }
    static Result<SystemDescriptionResolvedSources> Fail(string message) => Result<SystemDescriptionResolvedSources>.Failure(new RequestError(RequestErrorKind.Conflict, message));
}
