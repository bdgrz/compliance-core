using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

public sealed class GetTypeIBaselineHandler(IExaminationEngagementDirectoryReader directory)
    : IRequestHandler<GetTypeIBaseline, TypeIBaselineView>
{
    public async ValueTask<Result<TypeIBaselineView>> HandleAsync(
        IRequestContext<GetTypeIBaseline> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.TypeIBaselineId == Uuid.Empty)
            return Result<TypeIBaselineView>.Failure(new RequestError(
                RequestErrorKind.Validation, "Tenant, program, and baseline identities are required."));

        var baseline = await directory.GetBaselineAsync(request.TenantId,
            request.TypeIBaselineId, ct).ConfigureAwait(false);
        return baseline is { } view && view.TenantId == request.TenantId &&
               view.ProgramId == request.ProgramId && view.TypeIBaselineId == request.TypeIBaselineId
            ? Result<TypeIBaselineView>.Success(view)
            : Result<TypeIBaselineView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The Type I baseline was not found."));
    }
}
