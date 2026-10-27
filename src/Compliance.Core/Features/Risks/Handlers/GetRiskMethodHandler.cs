using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Queries;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks.Handlers;

/// <summary>Reads the small per-program method stream directly, so reads never lag writes.</summary>
public sealed class GetRiskMethodHandler(RiskMethodReader reader)
    : IRequestHandler<GetRiskMethod, RiskMethodVersionView>
{
    public async ValueTask<Result<RiskMethodVersionView>> HandleAsync(
        IRequestContext<GetRiskMethod> context, CancellationToken ct)
    {
        try
        {
            var version = await reader.CurrentAsync(context.Request.TenantId, context.Request.ProgramId, ct).ConfigureAwait(false);
            return Found(version);
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<RiskMethodVersionView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The exact risk method content cannot be verified.", error is ImmutableContentUnavailableException));
        }
    }

    internal static Result<RiskMethodVersionView> Found(RiskMethodVersionView? version) =>
        version is null
            ? Result<RiskMethodVersionView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The risk method version was not found."))
            : Result<RiskMethodVersionView>.Success(version);
}
