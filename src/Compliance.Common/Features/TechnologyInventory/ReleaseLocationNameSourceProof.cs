using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>Closed union of the record facts that prove a location name is no longer needed by its old claim holder.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(AppliedProof), "location_revision_recorded")]
[JsonDerivedType(typeof(CanceledProof), "location_change_canceled")]
public abstract record ReleaseLocationNameSourceProof
{
    private protected ReleaseLocationNameSourceProof()
    {
    }

    /// <summary>The proof of whichever source fact this variant carries.</summary>
    [JsonIgnore]
    public abstract SourceFactProof Proof { get; }

    /// <summary>The location revision that applied the change.</summary>
    public sealed record AppliedProof(SourceFactProof<LocationRevisionRecorded> Fact) : ReleaseLocationNameSourceProof
    {
        /// <inheritdoc />
        public override SourceFactProof Proof => Fact;
    }

    /// <summary>The cancellation that fenced the change.</summary>
    public sealed record CanceledProof(SourceFactProof<LocationChangeCanceled> Fact) : ReleaseLocationNameSourceProof
    {
        /// <inheritdoc />
        public override SourceFactProof Proof => Fact;
    }
}
