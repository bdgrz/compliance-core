using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     A verified closure: the authoritative finding, its accepted closure fact, the frozen closure intent and the
///     authored decision. Exactly one of <paramref name="Error" /> and the verified parts is set.
/// </summary>
public sealed record FindingClosure(RequestError? Error, FindingAggregate? Finding, NativeOperations.ClosureIntent? Intent,
    ClosureDecision? Decision, Uuid TenantId)
{
    /// <summary>A failed verification.</summary>
    public static FindingClosure Failed(RequestError error) => new(error, null, null, null, Uuid.Empty);
}
