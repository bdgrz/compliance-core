using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>Reads one adopted assessment composed from its own owner streams.</summary>
public sealed class GetReadinessAssessmentHandler(ReadinessReader readiness)
    : IRequestHandler<GetReadinessAssessment, ReadinessAssessmentView>
{
    public async ValueTask<Result<ReadinessAssessmentView>> HandleAsync(
        IRequestContext<GetReadinessAssessment> context, CancellationToken ct)
    {
        var request = context.Request;
        try
        {
            return await readiness.ReadViewAsync(request.TenantId, request.ProgramId, request.AssessmentId, ct)
                .ConfigureAwait(false) is { } view
                ? Result<ReadinessAssessmentView>.Success(view)
                : Result<ReadinessAssessmentView>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The readiness assessment was not found."));
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<ReadinessAssessmentView>.Failure(Unverified(exception));
        }
    }

    internal static RequestError Unverified(ImmutableContentIntegrityException exception) => new(
        RequestErrorKind.Conflict, $"The readiness content cannot be verified: {exception.Message}",
        exception is ImmutableContentUnavailableException);
}
