using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Examinations.Queries;
using Bdgrz.Compliance.Features.Examinations.Services;

namespace Bdgrz.Compliance.Features.Examinations.Handlers;

public sealed class GetExaminationHandler(IAggregateReader reader, ExaminationSetupAccess access)
    : IRequestHandler<GetExamination, ExaminationView>
{
    public async ValueTask<Result<ExaminationView>> HandleAsync(IRequestContext<GetExamination> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty || request.ExaminationId == Uuid.Empty)
            return Result<ExaminationView>.Failure(new RequestError(RequestErrorKind.Validation,
                "Select exact examination scope."));
        var actor = await access.RequireAsync(context, request.TenantId, request.ProgramId,
            RbacPermissions.TenantAccess, ct).ConfigureAwait(false);
        if (!actor.IsSuccess)
            return Result<ExaminationView>.Failure(actor.Error);
        var owner = await reader.HydrateAsync(new Examination(request.TenantId, request.ExaminationId), ct).ConfigureAwait(false);
        return owner.Current is { } state && state.ProgramId == request.ProgramId
            ? Result<ExaminationView>.Success(state)
            : Result<ExaminationView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The examination was not found in this program."));
    }
}
