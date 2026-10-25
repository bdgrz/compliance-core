using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>Reads the mapping line holding one proposal from its own owner stream, its authoritative source.</summary>
public sealed class GetControlCriterionMappingHandler(ControlMappingReader mappings)
    : IRequestHandler<GetControlCriterionMapping, ControlCriterionMappingView>
{
    public ValueTask<Result<ControlCriterionMappingView>> HandleAsync(
        IRequestContext<GetControlCriterionMapping> context, CancellationToken ct) =>
        MappingLineFailures.ExecuteAsync(() => HandleCoreAsync(context, ct), ct);

    async ValueTask<Result<ControlCriterionMappingView>> HandleCoreAsync(
        IRequestContext<GetControlCriterionMapping> context, CancellationToken ct)
    {
        var request = context.Request;
        return await mappings.ReadViewAsync(request.TenantId, request.ProgramId, request.MappingId, ct)
                .ConfigureAwait(false) is { } view
            ? Result<ControlCriterionMappingView>.Success(view)
            : Result<ControlCriterionMappingView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The control criterion mapping was not found."));
    }
}
