using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>Closed union of the record facts that prove a provider name is no longer needed by its old claim holder.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(RecordedProof), "provider_recorded")]
[JsonDerivedType(typeof(RevisedProof), "provider_revised")]
[JsonDerivedType(typeof(CanceledProof), "provider_change_canceled")]
public abstract record ReleaseProviderNameSourceProof
{
    private protected ReleaseProviderNameSourceProof()
    {
    }

    /// <summary>The proof of whichever source fact this variant carries.</summary>
    [JsonIgnore]
    public abstract SourceFactProof Proof { get; }

    /// <summary>The provider fact that created the provider.</summary>
    public sealed record RecordedProof(SourceFactProof<ProviderRecorded> Fact) : ReleaseProviderNameSourceProof
    {
        /// <inheritdoc />
        public override SourceFactProof Proof => Fact;
    }

    /// <summary>The provider revision that applied the change.</summary>
    public sealed record RevisedProof(SourceFactProof<ProviderRevised> Fact) : ReleaseProviderNameSourceProof
    {
        /// <inheritdoc />
        public override SourceFactProof Proof => Fact;
    }

    /// <summary>The cancellation that fenced the change.</summary>
    public sealed record CanceledProof(SourceFactProof<ProviderChangeCanceled> Fact) : ReleaseProviderNameSourceProof
    {
        /// <inheritdoc />
        public override SourceFactProof Proof => Fact;
    }
}
