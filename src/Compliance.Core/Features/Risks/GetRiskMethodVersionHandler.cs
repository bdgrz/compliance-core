using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public sealed class GetRiskMethodVersionHandler(RiskMethodReader reader)
    : IRequestHandler<GetRiskMethodVersion, RiskMethodVersionView>
{
    public async ValueTask<Result<RiskMethodVersionView>> HandleAsync(
        IRequestContext<GetRiskMethodVersion> context, CancellationToken ct)
    {
        try
        {
            var method = await reader.ReadAsync(context.Request.TenantId, context.Request.ProgramId, ct).ConfigureAwait(false);
            return GetRiskMethodHandler.Found(method.GetVersion(context.Request.Version));
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<RiskMethodVersionView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The exact risk method content cannot be verified.", error is ImmutableContentUnavailableException));
        }
    }
}
