using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

public sealed class FreezeTypeIBaselineHandler(IAggregateReader reader,
    IAggregateExecutor executor, IExaminationEngagementDirectoryReader directory,
    ITypeIBaselinePreviewReader previews, TimeProvider clock)
    : IRequestHandler<FreezeTypeIBaseline>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<FreezeTypeIBaseline> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!ExaminationManagementActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var memberId, out var display))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Freezing a Type I baseline requires an authenticated personal actor."));
        if (request.ExpectedRevision <= 0 || request.BaselineId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(request.ConfirmationRationale) ||
            request.ConfirmationRationale.Length > 2000 || !ValidDigest(request.ExpectedPreviewSha256))
            return Invalid("A baseline confirmation requires a stable identity, current revision, rationale, and preview digest.");

        var projected = await directory.GetAsync(request.TenantId, request.ExaminationId, ct)
            .ConfigureAwait(false);
        if (projected is null || projected.ProgramId != request.ProgramId ||
            projected.TenantId != request.TenantId ||
            projected.ExaminationEngagementId != request.ExaminationId)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The examination engagement was not found."));

        var engagement = await reader.HydrateAsync(new ExaminationEngagement(request.TenantId,
            request.ProgramId, request.ExaminationId), ct).ConfigureAwait(false);
        if (engagement.Current is not { } current)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The examination engagement was not found."));

        TypeIBaselinePreview preview;
        var readCurrentPreview = true;
        if (engagement.Baseline is { Status: "frozen" } existing &&
            existing.TypeIBaselineId == request.BaselineId)
        {
            preview = FromBaseline(existing);
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
            return Conflict("The Type I baseline preview changed. Review the current preview before confirming.");
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
                CommandFailureRequestAdapter.ToOutcome(aggregate.FreezeBaseline(
                    request.ExpectedRevision, request.BaselineId, preview,
                    request.ConfirmationRationale, memberId, display, clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }

    internal static TypeIBaselinePreview FromBaseline(TypeIBaselineView baseline) =>
        TypeIBaselinePreview.Create(baseline.TenantId, baseline.ProgramId,
            baseline.ExaminationEngagementId, baseline.Sources, [], baseline.ScopeDifferences,
            baseline.SourceCoverage);

    static bool ValidDigest(string? value) => value is { Length: 64 } && value.All(
        static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    static Result Invalid(string message) =>
        Result.Failure(new RequestError(RequestErrorKind.Validation, message));

    static Result Conflict(string message, bool transient = false) =>
        Result.Failure(new RequestError(RequestErrorKind.Conflict, message, isTransient: transient));
}
