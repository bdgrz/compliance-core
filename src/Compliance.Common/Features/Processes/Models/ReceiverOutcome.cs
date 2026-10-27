using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
namespace Bdgrz.Compliance.Features.Processes.Models;

/// <summary>
/// Closed result of every receiver command. A receiver either applied its fact, replayed an exact earlier
/// acceptance, or proved that a later generation superseded it (a terminal no-op). Transient waits and conflicting
/// intent are failures, not outcomes.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(Applied), "applied")]
[JsonDerivedType(typeof(Replayed), "replayed")]
[JsonDerivedType(typeof(Superseded), "superseded")]
[JsonDerivedType(typeof(Rejected), "rejected")]
[JsonDerivedType(typeof(Performed), "performed")]
public abstract record ReceiverOutcome
{
    private protected ReceiverOutcome()
    {
    }

    /// <summary>The receiver appended its fact on the target owner.</summary>
    public sealed record Applied(NativeFactReference Fact) : ReceiverOutcome;

    /// <summary>The exact same receiver command was already accepted; nothing was appended.</summary>
    public sealed record Replayed(NativeFactReference Fact) : ReceiverOutcome;

    /// <summary>A proven later generation makes this receiver a terminal no-op.</summary>
    public sealed record Superseded(string Reason, NativeFactReference? Evidence) : ReceiverOutcome;

    /// <summary>
    /// The receiver proved that its source operation can never proceed. The process driver aborts the process: abort
    /// receivers fence first, then only settle and release stages run. Unlike <see cref="Superseded" /> it does not end
    /// the process, so uses already held are still settled.
    /// </summary>
    public sealed record Rejected(string Reason, NativeFactReference? Evidence = null) : ReceiverOutcome;

    /// <summary>A technical effect without a native fact, such as a delivery, was performed or already recorded.</summary>
    public sealed record Performed(string Effect) : ReceiverOutcome;
}
