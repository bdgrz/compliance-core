using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public static class ProductClientServiceReadFailure
{
    public static RequestError Of(ImmutableContentIntegrityException error) => new(RequestErrorKind.Conflict,
        "The client service's exact immutable source cannot be verified.", error is ImmutableContentUnavailableException);
}
