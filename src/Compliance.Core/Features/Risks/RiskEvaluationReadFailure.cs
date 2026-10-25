using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Risks;

public static class RiskEvaluationReadFailure
{
    public static RequestError Of(ImmutableContentIntegrityException error) => new(RequestErrorKind.Conflict,
        "The risk evaluation's exact immutable source cannot be verified.", error is ImmutableContentUnavailableException);
}
