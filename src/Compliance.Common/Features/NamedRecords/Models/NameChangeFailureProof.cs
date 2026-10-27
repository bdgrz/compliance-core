using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.NamedRecords.Models;

/// <summary>
///     The definite failure that cancels a pending name change: the claim a receiver proved unavailable and the reason.
///     It is frozen as content so the cancellation fact carries its own proof.
/// </summary>
public sealed record NameChangeFailureProof(Uuid OperationId, long Generation, string Reason)
{
    /// <summary>Schema of the frozen proof content.</summary>
    public const string Schema = "name-change-failure-proof.v1";
}
