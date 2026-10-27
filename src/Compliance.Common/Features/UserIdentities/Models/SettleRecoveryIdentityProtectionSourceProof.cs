using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.UserIdentities.Events;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.UserIdentities.Models;

/// <summary>Closed union of the source facts that settle the protection of a recovery identity slot.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(CompletedProof), "recovery_completed")]
[JsonDerivedType(typeof(RejectedProof), "preparation_rejected")]
public abstract record SettleRecoveryIdentityProtectionSourceProof
{
    private protected SettleRecoveryIdentityProtectionSourceProof()
    {
    }

    /// <summary>The proof of whichever source fact this variant carries.</summary>
    [JsonIgnore]
    public abstract SourceFactProof Proof { get; }

    /// <summary>The completed swap releases the exact matching protection.</summary>
    public sealed record CompletedProof(SourceFactProof<IdentityRecoveryCompleted> Fact)
        : SettleRecoveryIdentityProtectionSourceProof
    {
        /// <inheritdoc />
        public override SourceFactProof Proof => Fact;
    }

    /// <summary>The rejected preparation fences the slot, including a slot that was never protected.</summary>
    public sealed record RejectedProof(SourceFactProof<IdentityRecoveryPreparationRejected> Fact)
        : SettleRecoveryIdentityProtectionSourceProof
    {
        /// <inheritdoc />
        public override SourceFactProof Proof => Fact;
    }
}
