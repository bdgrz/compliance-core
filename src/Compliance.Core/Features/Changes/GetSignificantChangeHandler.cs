using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

public sealed class GetSignificantChangeHandler(ISignificantChangeDirectoryReader directory,
    RestrictedComplianceRecordVisibility visibility)
    : IRequestHandler<GetSignificantChange, SignificantChangeView>
{
    public async ValueTask<Result<SignificantChangeView>> HandleAsync(
        IRequestContext<GetSignificantChange> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.ChangeId == Uuid.Empty)
            return Result<SignificantChangeView>.Failure(new RequestError(RequestErrorKind.Validation,
                "Tenant, program, and significant-change identities are required."));

        var change = await directory.GetAsync(request.TenantId, request.ChangeId, ct)
            .ConfigureAwait(false);
        return change is { Current: { } current } view && view.TenantId == request.TenantId &&
               view.ProgramId == request.ProgramId && view.SignificantChangeId == request.ChangeId &&
               await visibility.CanReadAsync(request.TenantId, context.Actor,
                   current.Content.HandlingClass,
                   view.Assessments.Select(static assessment => assessment.AssessedByMemberId).ToArray(), ct)
                   .ConfigureAwait(false)
            ? Result<SignificantChangeView>.Success(view)
            : Result<SignificantChangeView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The significant change was not found."));
    }
}
