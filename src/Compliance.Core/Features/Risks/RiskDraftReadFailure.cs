using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public static class RiskDraftReadFailure
{
    public static RequestError Of(ImmutableContentIntegrityException failure) => new(RequestErrorKind.Conflict,
        "The risk draft's exact immutable source cannot be verified.", failure is ImmutableContentUnavailableException);
}
