using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Builds a compliance-lead preview from an unshared draft and exact source versions.</summary>
public sealed class PreviewAdvisorHandoffHandler(AdvisorHandoffReader aggregates,
    IAdvisorRecordVersionResolver recordVersions, IAdvisorSharedSourceReader sources,
    TimeProvider clock)
    : IRequestHandler<PreviewAdvisorHandoff, AdvisorHandoffPreviewView>
{
    public ValueTask<Result<AdvisorHandoffPreviewView>> HandleAsync(
        IRequestContext<PreviewAdvisorHandoff> context, CancellationToken ct) =>
        AdvisorFailures.ClassifyAsync(() => HandleCoreAsync(context, ct), ct);

    async ValueTask<Result<AdvisorHandoffPreviewView>> HandleCoreAsync(
        IRequestContext<PreviewAdvisorHandoff> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.HandoffId == Uuid.Empty)
            return Invalid();

        var handoff = await aggregates.ReadAsync(request.TenantId,
            request.ProgramId, request.HandoffId, ct).ConfigureAwait(false);
        if (!handoff.IsCreated || handoff.ProgramId != request.ProgramId)
            return Missing();
        if (handoff.Status != "draft")
            return Result<AdvisorHandoffPreviewView>.Failure(new RequestError(
                RequestErrorKind.Conflict,
                "Only an unshared draft handoff can be previewed."));
        if (handoff.ExpiresOn < DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime))
            return Result<AdvisorHandoffPreviewView>.Failure(new RequestError(
                RequestErrorKind.Conflict,
                "An expired handoff draft cannot be shared."));
        if (handoff.Purpose is not { } purpose || handoff.UpdatedAt is null ||
            handoff.Selection.Count == 0)
            return Result<AdvisorHandoffPreviewView>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The advisor handoff draft is incomplete."));

        var selected = handoff.Selection;
        var exact = await recordVersions.RequireCurrentAsync(request.TenantId,
            request.ProgramId, selected, ct).ConfigureAwait(false);
        if (!exact.IsSuccess)
            return Result<AdvisorHandoffPreviewView>.Failure(exact.Error);

        IReadOnlyList<AdvisorRecordVersion> previousSelection = [];
        if (handoff.ComparedToHandoffId is { } comparedToId)
        {
            var previous = await aggregates.ReadAsync(request.TenantId,
            request.ProgramId, comparedToId, ct).ConfigureAwait(false);
            if (!previous.IsCreated || previous.ProgramId != request.ProgramId)
                return Missing();
            if (!previous.WasReadyForReview || previous.Status is not ("closed" or "revoked"))
                return Result<AdvisorHandoffPreviewView>.Failure(new RequestError(
                    RequestErrorKind.Conflict,
                    "The comparison handoff must have been shared and then closed or revoked."));
            previousSelection = previous.Selection;
            if (selected.Any(source =>
                    AdvisorHandoffSelectionComparison.Classify(source, previousSelection) ==
                    AdvisorHandoffSourceChange.Unchanged))
                return Result<AdvisorHandoffPreviewView>.Failure(new RequestError(
                    RequestErrorKind.Validation,
                    "A repeated handoff may include only new or changed source versions."));
        }

        var items = new List<AdvisorHandoffPreviewItemView>(selected.Count);
        foreach (var source in selected)
        {
            var content = await sources.GetAsync(request.TenantId, request.ProgramId,
                request.HandoffId, source, ct).ConfigureAwait(false);
            if (!content.IsSuccess)
                return Result<AdvisorHandoffPreviewView>.Failure(content.Error);
            var compared = previousSelection.FirstOrDefault(candidate =>
                AdvisorHandoffSelectionComparison.SameLogicalSource(candidate, source));
            var change = compared is null ? "new" : "changed";
            items.Add(new AdvisorHandoffPreviewItemView(source, change, compared, content.Value));
        }

        var current = await aggregates.ReadAsync(request.TenantId,
            request.ProgramId, request.HandoffId, ct).ConfigureAwait(false);
        if (!current.IsCreated || current.ProgramId != request.ProgramId ||
            current.Status != "draft" || current.Revision != handoff.Revision ||
            current.ComparedToHandoffId != handoff.ComparedToHandoffId ||
            !current.Selection.SequenceEqual(selected))
            return Result<AdvisorHandoffPreviewView>.Failure(new RequestError(
                RequestErrorKind.Conflict,
                "The advisor handoff changed while its preview was being prepared."));

        return Result<AdvisorHandoffPreviewView>.Success(new AdvisorHandoffPreviewView(
            request.TenantId, request.ProgramId, request.HandoffId,
            handoff.ComparedToHandoffId, handoff.Revision, purpose, handoff.ExpiresOn,
            items, AdvisorHandoffSelectionComparison.Removed(previousSelection, selected)));
    }

    static Result<AdvisorHandoffPreviewView> Invalid() =>
        Result<AdvisorHandoffPreviewView>.Failure(new RequestError(RequestErrorKind.Validation,
            "A handoff preview requires tenant, program, and handoff identities."));

    static Result<AdvisorHandoffPreviewView> Missing() =>
        Result<AdvisorHandoffPreviewView>.Failure(new RequestError(RequestErrorKind.NotFound,
            "The advisor handoff was not found in the requested program."));
}
