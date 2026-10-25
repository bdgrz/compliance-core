using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public static class ProgramReadFailure
{
    public static RequestError Of(ImmutableContentIntegrityException error) => new(RequestErrorKind.Conflict,
        "The program's exact immutable source cannot be verified.", error is ImmutableContentUnavailableException);
}
