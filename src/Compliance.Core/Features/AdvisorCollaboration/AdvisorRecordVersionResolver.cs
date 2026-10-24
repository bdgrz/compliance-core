using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.Readiness;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Checks the exact source version and program scope before a handoff can include it.</summary>
sealed class AdvisorRecordVersionResolver(IAggregateReader aggregates,
    EvidenceArtifactMetadataRead evidenceMetadata, EvidenceSupportTargetSource evidenceTargets,
    IBoundaryDirectoryReader boundaries, ReadinessReader readiness)
    : IAdvisorRecordVersionResolver
{
    public async ValueTask<Result> RequireCurrentAsync(Uuid tenantId, Uuid programId,
        IReadOnlyList<AdvisorRecordVersion> selection, CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || programId == Uuid.Empty ||
            selection is not { Count: > 0 and <= AdvisorHandoff.MaximumSelectionCount } ||
            selection.Any(static item => item is null || !item.IsSupported) ||
            selection.Select(static item => (item.SourceFamily, item.RecordId, item.EntryId))
                .Distinct().Count() != selection.Count)
            return InvalidSelection();

        foreach (var selected in selection)
        {
            var current = selected.SourceKind switch
            {
                AdvisorRecordVersion.ControlDraftKind => await RequireControlDraftAsync(tenantId,
                    programId, selected, ct).ConfigureAwait(false),
                AdvisorRecordVersion.ControlVersionKind => await RequireControlVersionAsync(tenantId,
                    programId, selected, ct).ConfigureAwait(false),
                AdvisorRecordVersion.PolicyDraftKind => await RequirePolicyDraftAsync(tenantId,
                    programId, selected, ct).ConfigureAwait(false),
                AdvisorRecordVersion.PolicyVersionKind => await RequirePolicyVersionAsync(tenantId,
                    programId, selected, ct).ConfigureAwait(false),
                AdvisorRecordVersion.EvidenceArtifactKind => await RequireEvidenceAsync(tenantId,
                    programId, selected, ct).ConfigureAwait(false),
                AdvisorRecordVersion.ReadinessGapKind => await RequireReadinessGapAsync(tenantId,
                    programId, selected, ct).ConfigureAwait(false),
                AdvisorRecordVersion.BoundaryQuestionKind => await RequireBoundaryQuestionAsync(
                    tenantId, programId, selected, ct).ConfigureAwait(false),
                _ => InvalidSelection(),
            };
            if (!current.IsSuccess)
                return current;
        }
        return Result.Success;
    }

    async ValueTask<Result> RequireControlDraftAsync(Uuid tenantId, Uuid programId,
        AdvisorRecordVersion selected, CancellationToken ct)
    {
        var control = await aggregates.HydrateAsync(new ControlDraft(tenantId, selected.RecordId), ct)
            .ConfigureAwait(false);
        if (!control.IsVisible || control.ProgramId != programId)
            return Missing();
        return control.HasOpenDraft && control.DraftVersionId == selected.VersionId &&
               control.Revision == selected.Revision
            ? Result.Success
            : Stale();
    }

    async ValueTask<Result> RequireControlVersionAsync(Uuid tenantId, Uuid programId,
        AdvisorRecordVersion selected, CancellationToken ct)
    {
        var control = await aggregates.HydrateAsync(new ControlDraft(tenantId, selected.RecordId), ct)
            .ConfigureAwait(false);
        if (!control.IsVisible || control.ProgramId != programId)
            return Missing();
        return control.ApprovedVersion is { Status: "approved" } current && !control.IsRetired &&
               current.TenantId == tenantId && current.ProgramId == programId &&
               current.VersionId == selected.VersionId && current.Revision == selected.Revision
            ? Result.Success
            : Stale();
    }

    async ValueTask<Result> RequirePolicyDraftAsync(Uuid tenantId, Uuid programId,
        AdvisorRecordVersion selected, CancellationToken ct)
    {
        var policy = await aggregates.HydrateAsync(new Policy(tenantId, selected.RecordId), ct)
            .ConfigureAwait(false);
        if (!policy.IsVisible || policy.ProgramId != programId)
            return Missing();
        return policy.DraftContent is not null && policy.Revision == selected.Revision
            ? Result.Success
            : Stale();
    }

    async ValueTask<Result> RequirePolicyVersionAsync(Uuid tenantId, Uuid programId,
        AdvisorRecordVersion selected, CancellationToken ct)
    {
        var policy = await aggregates.HydrateAsync(new Policy(tenantId, selected.RecordId), ct)
            .ConfigureAwait(false);
        if (!policy.IsVisible || policy.ProgramId != programId)
            return Missing();
        return policy.CurrentVersion is { Status: "approved" } current && !policy.IsRetired &&
               current.TenantId == tenantId && current.ProgramId == programId &&
               current.Version == selected.VersionNumber && current.Revision == selected.Revision
            ? Result.Success
            : Stale();
    }

    async ValueTask<Result> RequireEvidenceAsync(Uuid tenantId, Uuid programId,
        AdvisorRecordVersion selected, CancellationToken ct)
    {
        var support = await aggregates.HydrateAsync(new EvidenceSupport(tenantId, programId,
            selected.EvidenceSupportId!.Value), ct).ConfigureAwait(false);
        if (support.Current is not { } link || link.TenantId != tenantId ||
            link.ProgramId != programId || link.SubjectId != selected.EvidenceSupportId ||
            link.ArtifactId != selected.RecordId)
            return Missing();

        var request = new LinkEvidenceSupport(tenantId, programId, link.ArtifactId,
            link.SupportedRecordType!, link.SupportedRecordId, link.SupportedRecordRevision,
            link.PeriodStart, link.PeriodEnd, link.Purpose!, link.SufficiencyRationale!);
        if (EvidenceSupport.IdFor(request) != selected.EvidenceSupportId)
            return Missing();
        if (link.ArtifactRevision != selected.Revision ||
            link.ArtifactRevision <= 0 || link.ArtifactContentSha256 != selected.ContentSha256)
            return Stale();

        var exactTarget = await evidenceTargets.RequireExactAsync(request, ct).ConfigureAwait(false);
        if (!exactTarget.IsSuccess)
            return exactTarget;

        var metadata = await evidenceMetadata.GetAsync(tenantId, selected.RecordId, ct)
            .ConfigureAwait(false);
        if (!metadata.IsSuccess)
            return metadata.Error.Kind == RequestErrorKind.NotFound
                ? Missing()
                : Result.Failure(metadata.Error);
        if (metadata.Value.State != EvidenceArtifactStates.Available ||
            metadata.Value.Content is null || selected.SourcePosition != (ulong)link.ArtifactRevision ||
            metadata.Value.SourcePosition < selected.SourcePosition ||
            metadata.Value.ContentSha256 != selected.ContentSha256)
            return Stale();
        return await evidenceMetadata.CheckAsync(metadata.Value, ct).ConfigureAwait(false);
    }

    async ValueTask<Result> RequireReadinessGapAsync(Uuid tenantId, Uuid programId,
        AdvisorRecordVersion selected, CancellationToken ct)
    {
        ReadinessAssessmentView? assessment;
        try
        {
            if (await readiness.GetLatestAssessmentIdAsync(tenantId, programId, ct).ConfigureAwait(false) !=
                selected.AssessmentId)
                return Stale();
            assessment = await readiness.ReadViewAsync(tenantId, programId, selected.AssessmentId!.Value, ct)
                .ConfigureAwait(false);
        }
        catch (Content.ImmutableContentIntegrityException exception)
        {
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The readiness content cannot be verified yet: {exception.Message}", true));
        }

        if (assessment is null || assessment.TenantId != tenantId ||
            assessment.ProgramId != programId)
            return Missing();
        return assessment.InputFingerprint == selected.InputFingerprint &&
               assessment.Gaps.Any(gap => gap.GapId == selected.RecordId)
            ? Result.Success
            : Stale();
    }

    async ValueTask<Result> RequireBoundaryQuestionAsync(Uuid tenantId, Uuid programId,
        AdvisorRecordVersion selected, CancellationToken ct)
    {
        var boundary = await boundaries.GetAsync(tenantId, selected.RecordId, ct)
            .ConfigureAwait(false);
        if (boundary is null || boundary.TenantId != tenantId ||
            boundary.BoundaryId != selected.RecordId || boundary.ProgramId != programId)
            return Missing();

        var selectedVersion = boundary.Draft is { } draft &&
                              draft.VersionId == selected.VersionId &&
                              draft.Revision == selected.Revision
            ? draft
            : boundary.LatestApprovedVersion is { Status: "approved" } approved &&
              approved.VersionId == selected.VersionId && approved.Revision == selected.Revision
                ? approved
                : null;
        if (selectedVersion is null || selectedVersion.TenantId != tenantId ||
            selectedVersion.ProgramId != programId || selectedVersion.BoundaryId != selected.RecordId ||
            selectedVersion.Revision != selected.Revision)
            return Stale();
        return selectedVersion.Content.Entries.Any(entry => entry.EntryId == selected.EntryId &&
                entry.Kind == "question")
            ? Result.Success
            : Stale();
    }

    static Result InvalidSelection() => Result.Failure(new RequestError(RequestErrorKind.Validation,
        "A handoff must select a bounded set of supported exact source versions."));

    static Result Missing() => Result.Failure(new RequestError(RequestErrorKind.NotFound,
        "A selected source was not found in the requested program."));

    static Result Stale() => Result.Failure(new RequestError(RequestErrorKind.Conflict,
        "A selected source version is no longer current."));
}
