using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>Reads the applicability line holding one proposal from its own owner stream, its authoritative source.</summary>
public sealed class GetCriterionApplicabilityHandler(ApplicabilityReader applicability)
    : IRequestHandler<GetCriterionApplicability, CriterionApplicabilityView>
{
    public ValueTask<Result<CriterionApplicabilityView>> HandleAsync(
        IRequestContext<GetCriterionApplicability> context, CancellationToken ct) =>
        MappingLineFailures.ExecuteAsync(() => HandleCoreAsync(context, ct), ct);

    async ValueTask<Result<CriterionApplicabilityView>> HandleCoreAsync(
        IRequestContext<GetCriterionApplicability> context, CancellationToken ct)
    {
        var request = context.Request;
        return await applicability.ReadViewAsync(request.TenantId, request.ProgramId, request.DecisionId, ct)
                .ConfigureAwait(false) is { } view
            ? Result<CriterionApplicabilityView>.Success(view)
            : Result<CriterionApplicabilityView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The criterion applicability decision was not found."));
    }
}
