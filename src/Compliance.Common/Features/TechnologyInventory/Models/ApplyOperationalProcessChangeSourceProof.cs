using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Events;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.TechnologyInventory.Models;

/// <summary>
///     Closed union of the facts that can carry an applied operational process change: a held name claim, or the accepted change itself
///     when the change needs no claim (a retired operational process stays retired).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(ClaimedProof), "operational_process_name_claimed")]
[JsonDerivedType(typeof(AcceptedProof), "operational_process_change_accepted")]
public abstract record ApplyOperationalProcessChangeSourceProof
{
    private protected ApplyOperationalProcessChangeSourceProof()
    {
    }

    /// <summary>The proof of whichever source fact this variant carries.</summary>
    [JsonIgnore]
    public abstract SourceFactProof Proof { get; }

    /// <summary>A claim the record holds for this change.</summary>
    public sealed record ClaimedProof(SourceFactProof<OperationalProcessNameClaimed> Fact) : ApplyOperationalProcessChangeSourceProof
    {
        /// <inheritdoc />
        public override SourceFactProof Proof => Fact;
    }

    /// <summary>The accepted change, for a change that acquires no claim.</summary>
    public sealed record AcceptedProof(SourceFactProof<OperationalProcessChangeAccepted> Fact) : ApplyOperationalProcessChangeSourceProof
    {
        /// <inheritdoc />
        public override SourceFactProof Proof => Fact;
    }
}
