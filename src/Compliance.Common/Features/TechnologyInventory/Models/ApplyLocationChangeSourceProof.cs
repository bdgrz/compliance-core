using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Events;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.TechnologyInventory.Models;

/// <summary>
///     Closed union of the facts that can carry an applied location change: a held name claim, or the accepted change itself
///     when the change needs no claim (a retired location stays retired).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(ClaimedProof), "location_name_claimed")]
[JsonDerivedType(typeof(AcceptedProof), "location_change_accepted")]
public abstract record ApplyLocationChangeSourceProof
{
    private protected ApplyLocationChangeSourceProof()
    {
    }

    /// <summary>The proof of whichever source fact this variant carries.</summary>
    [JsonIgnore]
    public abstract SourceFactProof Proof { get; }

    /// <summary>A claim the record holds for this change.</summary>
    public sealed record ClaimedProof(SourceFactProof<LocationNameClaimed> Fact) : ApplyLocationChangeSourceProof
    {
        /// <inheritdoc />
        public override SourceFactProof Proof => Fact;
    }

    /// <summary>The accepted change, for a change that acquires no claim.</summary>
    public sealed record AcceptedProof(SourceFactProof<LocationChangeAccepted> Fact) : ApplyLocationChangeSourceProof
    {
        /// <inheritdoc />
        public override SourceFactProof Proof => Fact;
    }
}
