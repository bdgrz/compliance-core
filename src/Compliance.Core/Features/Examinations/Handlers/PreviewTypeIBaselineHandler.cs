using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Examinations.Queries;
using Bdgrz.Compliance.Features.Examinations.Readers;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations.Handlers;

public sealed class PreviewTypeIBaselineHandler(ExaminationEngagementReader examinations,
    IExaminationEngagementDirectoryReader directory, ITypeIBaselinePreviewReader previews)
    : IRequestHandler<PreviewTypeIBaseline, TypeIBaselinePreviewView>
{
    public ValueTask<Result<TypeIBaselinePreviewView>> HandleAsync(
        IRequestContext<PreviewTypeIBaseline> context, CancellationToken ct) =>
        ExaminationSourceFailures.ClassifyAsync(() => HandleCoreAsync(context, ct));

    async ValueTask<Result<TypeIBaselinePreviewView>> HandleCoreAsync(
        IRequestContext<PreviewTypeIBaseline> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.ExaminationId == Uuid.Empty)
            return Failure(RequestErrorKind.Validation,
                "Tenant, program, and examination identities are required.");

        var projected = await directory.GetAsync(request.TenantId, request.ExaminationId, ct)
            .ConfigureAwait(false);
        if (projected is null || projected.TenantId != request.TenantId ||
            projected.ProgramId != request.ProgramId ||
            projected.ExaminationEngagementId != request.ExaminationId)
            return Failure(RequestErrorKind.NotFound, "The examination engagement was not found.");

        var aggregate = await examinations.ReadAsync(request.TenantId, projected.ProgramId, request.ExaminationId, ct)
            .ConfigureAwait(false);
        if (aggregate.View is not { } current)
            return Failure(RequestErrorKind.Conflict,
                "The examination engagement source has not reached its projection.", true);
        if (!ExaminationEngagementSourceMatch.Matches(projected, current))
            return Failure(RequestErrorKind.Conflict,
                "The examination engagement projection changed or has not caught up. Retry the preview.",
                true);

        var requesterUserId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject : Uuid.Empty;
        var preview = await previews.ReadAsync(current, requesterUserId, ct).ConfigureAwait(false);
        if (!preview.IsSuccess)
            return Result<TypeIBaselinePreviewView>.Failure(preview.Error);
        if (preview.Value.TenantId != request.TenantId ||
            preview.Value.ProgramId != request.ProgramId ||
            preview.Value.ExaminationEngagementId != request.ExaminationId)
            return Failure(RequestErrorKind.Conflict,
                "The baseline source reader returned a preview for a different engagement.");

        var confirmed = await examinations.ReadAsync(request.TenantId, projected.ProgramId, request.ExaminationId, ct)
            .ConfigureAwait(false);
        if (confirmed.View is not { } afterRead ||
            !ExaminationEngagementSourceMatch.Matches(current, afterRead))
            return Failure(RequestErrorKind.Conflict,
                "The examination engagement changed while its baseline preview was being read. Retry the preview.",
                true);

        return Result<TypeIBaselinePreviewView>.Success(new TypeIBaselinePreviewView(
            preview.Value.TenantId, preview.Value.ProgramId,
            preview.Value.ExaminationEngagementId, current.Revision, current.PeriodStart,
            Array.AsReadOnly(preview.Value.Sources.ToArray()),
            Array.AsReadOnly(preview.Value.SourceCoverage.ToArray()),
            Array.AsReadOnly(preview.Value.Blockers.ToArray()),
            Array.AsReadOnly(preview.Value.ScopeDifferences.ToArray()),
            preview.Value.CanonicalManifest, preview.Value.ContentSha256));
    }

    static Result<TypeIBaselinePreviewView> Failure(RequestErrorKind kind, string message,
        bool transient = false) => Result<TypeIBaselinePreviewView>.Failure(
        new RequestError(kind, message, isTransient: transient));
}
