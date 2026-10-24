using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

static class BoundaryReadFailure
{
    public static RequestError Of(ImmutableContentIntegrityException error) => new(RequestErrorKind.Conflict,
        "The boundary immutable content cannot be verified.", error is ImmutableContentUnavailableException);
}
