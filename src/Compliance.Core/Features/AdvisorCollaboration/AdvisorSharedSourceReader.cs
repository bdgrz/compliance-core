using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.Readiness;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

sealed class AdvisorSharedSourceReader(IAggregateReader aggregates, ControlReader controlSources,
    IAdvisorRecordVersionResolver versions, EvidenceArtifactMetadataRead evidence,
    IBoundaryDirectoryReader boundaries, ReadinessReader readiness)
    : IAdvisorSharedSourceReader
{
    public ValueTask<Result<AdvisorSharedSourceView>> GetAsync(Uuid tenantId,
        Uuid programId, Uuid handoffId, AdvisorRecordVersion selectedSource,
        CancellationToken ct = default) =>
        ControlSourceFailures.ClassifyAsync(() => GetCoreAsync(tenantId, programId, handoffId, selectedSource, ct));

    async ValueTask<Result<AdvisorSharedSourceView>> GetCoreAsync(Uuid tenantId,
        Uuid programId, Uuid handoffId, AdvisorRecordVersion selectedSource,
        CancellationToken ct = default)
    {
        var current = await versions.RequireCurrentAsync(tenantId, programId, [selectedSource], ct)
            .ConfigureAwait(false);
        if (!current.IsSuccess)
            return Result<AdvisorSharedSourceView>.Failure(current.Error);

        var view = selectedSource.SourceKind switch
        {
            AdvisorRecordVersion.ControlDraftKind => await ReadControlDraftAsync(tenantId,
                programId, handoffId, selectedSource, ct).ConfigureAwait(false),
            AdvisorRecordVersion.ControlVersionKind => await ReadControlVersionAsync(tenantId,
                programId, handoffId, selectedSource, ct).ConfigureAwait(false),
            AdvisorRecordVersion.PolicyDraftKind => await ReadPolicyDraftAsync(tenantId,
                programId, handoffId, selectedSource, ct).ConfigureAwait(false),
            AdvisorRecordVersion.PolicyVersionKind => await ReadPolicyVersionAsync(tenantId,
                programId, handoffId, selectedSource, ct).ConfigureAwait(false),
            AdvisorRecordVersion.EvidenceArtifactKind => await ReadEvidenceAsync(tenantId,
                programId, handoffId, selectedSource, ct).ConfigureAwait(false),
            AdvisorRecordVersion.ReadinessGapKind => await ReadReadinessGapAsync(tenantId,
                programId, handoffId, selectedSource, ct).ConfigureAwait(false),
            AdvisorRecordVersion.BoundaryQuestionKind => await ReadBoundaryQuestionAsync(tenantId,
                programId, handoffId, selectedSource, ct).ConfigureAwait(false),
            _ => InvalidSource(),
        };
        if (!view.IsSuccess)
            return view;

        current = await versions.RequireCurrentAsync(tenantId, programId, [selectedSource], ct)
            .ConfigureAwait(false);
        return current.IsSuccess
            ? view
            : Result<AdvisorSharedSourceView>.Failure(current.Error);
    }

    async ValueTask<Result<AdvisorSharedSourceView>> ReadControlDraftAsync(Uuid tenantId,
        Uuid programId, Uuid handoffId, AdvisorRecordVersion selected, CancellationToken ct)
    {
        var control = await controlSources.ReadAsync(tenantId, programId, selected.RecordId, ct)
            .ConfigureAwait(false);
        return control.CurrentContent is { } content
            ? Success(tenantId, programId, handoffId, selected, controlDraft: content)
            : StaleSource();
    }

    async ValueTask<Result<AdvisorSharedSourceView>> ReadControlVersionAsync(Uuid tenantId,
        Uuid programId, Uuid handoffId, AdvisorRecordVersion selected, CancellationToken ct)
    {
        var control = await controlSources.ReadAsync(tenantId, programId, selected.RecordId, ct)
            .ConfigureAwait(false);
        var version = control.ReadVersions().FirstOrDefault(item =>
            item.VersionId == selected.VersionId && item.Revision == selected.Revision);
        return version is not null
            ? Success(tenantId, programId, handoffId, selected, controlVersion: version)
            : StaleSource();
    }

    async ValueTask<Result<AdvisorSharedSourceView>> ReadPolicyDraftAsync(Uuid tenantId,
        Uuid programId, Uuid handoffId, AdvisorRecordVersion selected, CancellationToken ct)
    {
        var policy = await aggregates.HydrateAsync(new Policy(tenantId, selected.RecordId), ct)
            .ConfigureAwait(false);
        return policy.DraftContent is { } content
            ? Success(tenantId, programId, handoffId, selected, policyDraft: content)
            : StaleSource();
    }

    async ValueTask<Result<AdvisorSharedSourceView>> ReadPolicyVersionAsync(Uuid tenantId,
        Uuid programId, Uuid handoffId, AdvisorRecordVersion selected, CancellationToken ct)
    {
        var policy = await aggregates.HydrateAsync(new Policy(tenantId, selected.RecordId), ct)
            .ConfigureAwait(false);
        var version = policy.ReadVersions().FirstOrDefault(item =>
            item.Version == selected.VersionNumber && item.Revision == selected.Revision);
        return version is not null
            ? Success(tenantId, programId, handoffId, selected, policyVersion: version)
            : StaleSource();
    }

    async ValueTask<Result<AdvisorSharedSourceView>> ReadEvidenceAsync(Uuid tenantId,
        Uuid programId, Uuid handoffId, AdvisorRecordVersion selected, CancellationToken ct)
    {
        var artifact = await evidence.GetAsync(tenantId, selected.RecordId, ct).ConfigureAwait(false);
        if (!artifact.IsSuccess)
            return Result<AdvisorSharedSourceView>.Failure(artifact.Error);
        return Success(tenantId, programId, handoffId, selected,
            evidenceArtifact: artifact.Value);
    }

    async ValueTask<Result<AdvisorSharedSourceView>> ReadReadinessGapAsync(Uuid tenantId,
        Uuid programId, Uuid handoffId, AdvisorRecordVersion selected, CancellationToken ct)
    {
        ReadinessAssessmentView? assessment;
        try
        {
            assessment = await readiness.ReadViewAsync(tenantId, programId, selected.AssessmentId!.Value, ct)
                .ConfigureAwait(false);
        }
        catch (Content.ImmutableContentIntegrityException exception)
        {
            return Result<AdvisorSharedSourceView>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The readiness content cannot be verified yet: {exception.Message}", true));
        }

        var gap = assessment?.Gaps.FirstOrDefault(item => item.GapId == selected.RecordId);
        return gap is not null
            ? Success(tenantId, programId, handoffId, selected, readinessGap: gap)
            : StaleSource();
    }

    async ValueTask<Result<AdvisorSharedSourceView>> ReadBoundaryQuestionAsync(Uuid tenantId,
        Uuid programId, Uuid handoffId, AdvisorRecordVersion selected, CancellationToken ct)
    {
        var boundary = await boundaries.GetAsync(tenantId, selected.RecordId, ct)
            .ConfigureAwait(false);
        if (boundary is null || boundary.TenantId != tenantId || boundary.ProgramId != programId)
            return MissingSource();
        var version = boundary.Draft is { } draft && draft.VersionId == selected.VersionId &&
                      draft.Revision == selected.Revision
            ? draft
            : await boundaries.GetVersionAsync(tenantId, selected.RecordId, selected.VersionId, ct)
                .ConfigureAwait(false);
        var question = version?.Content.Entries.FirstOrDefault(item =>
            item.EntryId == selected.EntryId && item.Kind == "question");
        return question is not null
            ? Success(tenantId, programId, handoffId, selected, boundaryQuestion: question)
            : StaleSource();
    }

    static Result<AdvisorSharedSourceView> Success(Uuid tenantId, Uuid programId,
        Uuid handoffId, AdvisorRecordVersion selected,
        ControlDraftContent? controlDraft = null, ControlVersionView? controlVersion = null,
        PolicyContent? policyDraft = null, PolicyVersionView? policyVersion = null,
        EvidenceArtifactMetadataView? evidenceArtifact = null,
        ReadinessGapView? readinessGap = null, BoundaryScopeEntry? boundaryQuestion = null) =>
        Result<AdvisorSharedSourceView>.Success(new AdvisorSharedSourceView(tenantId, programId,
            handoffId, selected, controlDraft, controlVersion, policyDraft, policyVersion,
            evidenceArtifact, readinessGap, boundaryQuestion));

    static Result<AdvisorSharedSourceView> InvalidSource() =>
        Result<AdvisorSharedSourceView>.Failure(new RequestError(RequestErrorKind.Validation,
            "The selected source type is not supported."));

    static Result<AdvisorSharedSourceView> MissingSource() =>
        Result<AdvisorSharedSourceView>.Failure(new RequestError(RequestErrorKind.NotFound,
            "The selected source was not found in the requested program."));

    static Result<AdvisorSharedSourceView> StaleSource() =>
        Result<AdvisorSharedSourceView>.Failure(new RequestError(RequestErrorKind.Conflict,
            "The selected source version is no longer current."));
}
