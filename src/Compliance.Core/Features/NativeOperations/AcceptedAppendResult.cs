using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.NativeOperations;

/// <summary>Closed outcome of a per-first-owner native acceptance.</summary>
public abstract record AcceptedAppendResult
{
    private protected AcceptedAppendResult()
    {
    }

    /// <summary>Conflict error returned for a logical id bound to different intent.</summary>
    public static RequestError ConflictError { get; } = new(RequestErrorKind.Conflict,
        "This operation identity already has different accepted intent.", false);

    /// <summary>The batch committed; the next position is the exclusive append frontier of the first owner.</summary>
    public sealed record Committed(NativeFactReference FirstFact, ulong NextPhysicalPositionExclusive,
        ImmutableContentRef FirstResponse, FirstResponseDescriptor Response) : AcceptedAppendResult;

    /// <summary>The binding was already accepted with equal fields; nothing was appended.</summary>
    public sealed record Replayed(NativeFactReference FirstFact, ImmutableContentRef FirstResponse,
        FirstResponseDescriptor Response) : AcceptedAppendResult;

    /// <summary>The logical id is bound to different intent on this owner; nothing was appended.</summary>
    public sealed record Conflict : AcceptedAppendResult
    {
        /// <summary>The single conflict instance.</summary>
        public static Conflict Instance { get; } = new();
    }
}
