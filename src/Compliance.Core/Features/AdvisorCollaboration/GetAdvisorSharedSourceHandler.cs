using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Returns one source's current content only through an unexpired open handoff.</summary>
public sealed class GetAdvisorSharedSourceHandler(AdvisorHandoffReader aggregates,
    IAdvisorSharedSourceReader sources, TimeProvider clock)
    : IRequestHandler<GetAdvisorSharedSource, AdvisorSharedSourceView>
{
    static readonly HashSet<string> SourceFamilies = new(StringComparer.Ordinal)
    {
        "control", "policy", AdvisorRecordVersion.EvidenceArtifactKind,
        AdvisorRecordVersion.ReadinessGapKind, AdvisorRecordVersion.BoundaryQuestionKind,
    };

    public async ValueTask<Result<AdvisorSharedSourceView>> HandleAsync(
        IRequestContext<GetAdvisorSharedSource> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.HandoffId == Uuid.Empty || request.RecordId == Uuid.Empty ||
            !SourceFamilies.Contains(request.SourceFamily) ||
            request.EntryId == Uuid.Empty)
            return Invalid();

        var handoff = await aggregates.ReadAsync(request.TenantId,
            request.ProgramId, request.HandoffId, ct).ConfigureAwait(false);
        if (!handoff.IsCreated || handoff.ProgramId != request.ProgramId ||
            handoff.Status != "ready_for_review" || handoff.ExpiresOn <
            DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime))
            return Missing();

        var selected = handoff.Selection.SingleOrDefault(source =>
            source.SourceFamily == request.SourceFamily && source.RecordId == request.RecordId &&
            source.EntryId == request.EntryId);
        if (selected is null)
            return Missing();
        var source = await sources.GetAsync(request.TenantId, request.ProgramId,
            request.HandoffId, selected, ct).ConfigureAwait(false);
        if (!source.IsSuccess)
            return source;

        handoff = await aggregates.ReadAsync(request.TenantId,
            request.ProgramId, request.HandoffId, ct).ConfigureAwait(false);
        return handoff.IsCreated && handoff.ProgramId == request.ProgramId &&
               handoff.Status == "ready_for_review" && handoff.ExpiresOn >=
               DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime) &&
               handoff.Selection.Contains(selected)
            ? source
            : Missing();
    }

    static Result<AdvisorSharedSourceView> Invalid() =>
        Result<AdvisorSharedSourceView>.Failure(new RequestError(RequestErrorKind.Validation,
            "A shared-source read requires a handoff, source family, and record identity."));

    static Result<AdvisorSharedSourceView> Missing() =>
        Result<AdvisorSharedSourceView>.Failure(new RequestError(RequestErrorKind.NotFound,
            "The selected source is not available through the requested handoff."));
}
