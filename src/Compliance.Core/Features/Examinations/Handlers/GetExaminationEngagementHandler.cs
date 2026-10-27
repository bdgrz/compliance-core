using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

public sealed class GetExaminationEngagementHandler(ExaminationEngagementReader examinations)
    : IRequestHandler<GetExaminationEngagement, ExaminationEngagementView>
{
    public ValueTask<Result<ExaminationEngagementView>> HandleAsync(
        IRequestContext<GetExaminationEngagement> context, CancellationToken ct) =>
        ExaminationSourceFailures.ClassifyAsync(() => HandleCoreAsync(context, ct));

    async ValueTask<Result<ExaminationEngagementView>> HandleCoreAsync(
        IRequestContext<GetExaminationEngagement> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.ExaminationEngagementId == Uuid.Empty)
            return Result<ExaminationEngagementView>.Failure(new RequestError(
                RequestErrorKind.Validation, "Tenant, program, and examination identities are required."));

        var engagement = (await examinations.ReadAsync(request.TenantId, request.ProgramId,
            request.ExaminationEngagementId, ct).ConfigureAwait(false)).View;
        return engagement is { } view && view.TenantId == request.TenantId &&
               view.ProgramId == request.ProgramId &&
               view.ExaminationEngagementId == request.ExaminationEngagementId
            ? Result<ExaminationEngagementView>.Success(view)
            : Result<ExaminationEngagementView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The examination engagement was not found."));
    }
}
