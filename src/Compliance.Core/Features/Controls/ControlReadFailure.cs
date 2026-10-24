using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

static class ControlReadFailure
{
    public static RequestError Of(ImmutableContentIntegrityException error) => new(RequestErrorKind.Conflict,
        "The control's immutable content cannot be verified.", isTransient: error is ImmutableContentUnavailableException);
}
