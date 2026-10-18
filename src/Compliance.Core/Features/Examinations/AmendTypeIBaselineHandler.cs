using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

public sealed class AmendTypeIBaselineHandler(IAggregateReader reader,
    IAggregateExecutor executor, IExaminationEngagementDirectoryReader directory,
    ITypeIBaselinePreviewReader previews, TimeProvider clock)
    : IRequestHandler<AmendTypeIBaseline>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<AmendTypeIBaseline> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!ExaminationManagementActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var memberId, out var display))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Amending a Type I baseline requires an authenticated personal actor."));
        if (request.ExpectedRevision <= 0 || request.BaselineId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 2000 ||
            request.ChangedRecordIds is null || request.AffectedPackageMaterialIds is null ||
            !ValidDigest(request.ExpectedPreviewSha256))
            return Invalid("A baseline amendment requires a stable identity, revision, reason, impact lists, and preview digest.");

        var projected = await directory.GetAsync(request.TenantId, request.ExaminationId, ct)
            .ConfigureAwait(false);
        if (projected is null || projected.ProgramId != request.ProgramId ||
            projected.TenantId != request.TenantId ||
            projected.ExaminationEngagementId != request.ExaminationId)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "A frozen Type I baseline was not found."));

        var engagement = await reader.HydrateAsync(new ExaminationEngagement(request.TenantId,
            request.ProgramId, request.ExaminationId), ct).ConfigureAwait(false);
        if (engagement.Current is not { } current || engagement.Baseline is null)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "A frozen Type I baseline was not found."));

        TypeIBaselinePreview preview;
        var readCurrentPreview = true;
        var priorAmendment = engagement.BaselineHistory.FirstOrDefault(baseline =>
            baseline.TypeIBaselineId == request.BaselineId);
        if (priorAmendment is not null)
        {
            preview = FreezeTypeIBaselineHandler.FromBaseline(priorAmendment);
            readCurrentPreview = false;
        }
        else
        {
            var result = await previews.ReadAsync(current, ct).ConfigureAwait(false);
            if (!result.IsSuccess)
                return Result.Failure(result.Error);
            preview = result.Value;
        }
        if (!StringComparer.Ordinal.Equals(preview.ContentSha256, request.ExpectedPreviewSha256))
            return Conflict("The Type I baseline preview changed. Review the current preview before amending.");
        if (readCurrentPreview)
        {
            var confirmed = await reader.HydrateAsync(new ExaminationEngagement(request.TenantId,
                request.ProgramId, request.ExaminationId), ct).ConfigureAwait(false);
            if (confirmed.Current is not { } afterRead ||
                !ExaminationEngagementSourceMatch.Matches(current, afterRead))
                return Conflict("The examination engagement changed during preview confirmation. Retry the command.",
                    transient: true);
        }

        return await executor.ExecuteAsync(new ExaminationEngagement(request.TenantId,
                request.ProgramId, request.ExaminationId), aggregate =>
                CommandFailureRequestAdapter.ToOutcome(aggregate.AmendBaseline(
                    request.ExpectedRevision, request.BaselineId, preview,
                    request.ChangedRecordIds, request.AffectedPackageMaterialIds, request.Reason,
                    memberId, display, clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }

    static bool ValidDigest(string? value) => value is { Length: 64 } && value.All(
        static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    static Result Invalid(string message) =>
        Result.Failure(new RequestError(RequestErrorKind.Validation, message));

    static Result Conflict(string message, bool transient = false) =>
        Result.Failure(new RequestError(RequestErrorKind.Conflict, message, isTransient: transient));
}
