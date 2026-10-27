using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

public static class SystemDescriptionReadFailure
{
    public static RequestError Of(ImmutableContentIntegrityException exception) => new(RequestErrorKind.Conflict,
        "The system description's exact immutable source cannot be verified.", exception is ImmutableContentUnavailableException);
}
