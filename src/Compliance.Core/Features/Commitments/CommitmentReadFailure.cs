using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

static class CommitmentReadFailure
{
    public static RequestError Of(ImmutableContentIntegrityException error) => new(RequestErrorKind.Conflict,
        "The commitment immutable content cannot be verified.", error is ImmutableContentUnavailableException);
}
