using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Events;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Models;

/// <summary>Closed union of the record facts that prove a operational process name is no longer needed by its old claim holder.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(AppliedProof), "operational_process_revision_recorded")]
[JsonDerivedType(typeof(CanceledProof), "operational_process_change_canceled")]
public abstract record ReleaseOperationalProcessNameSourceProof
{
    private protected ReleaseOperationalProcessNameSourceProof()
    {
    }

    /// <summary>The proof of whichever source fact this variant carries.</summary>
    [JsonIgnore]
    public abstract SourceFactProof Proof { get; }

    /// <summary>The operational process revision that applied the change.</summary>
    public sealed record AppliedProof(SourceFactProof<OperationalProcessRevisionRecorded> Fact) : ReleaseOperationalProcessNameSourceProof
    {
        /// <inheritdoc />
        public override SourceFactProof Proof => Fact;
    }

    /// <summary>The cancellation that fenced the change.</summary>
    public sealed record CanceledProof(SourceFactProof<OperationalProcessChangeCanceled> Fact) : ReleaseOperationalProcessNameSourceProof
    {
        /// <inheritdoc />
        public override SourceFactProof Proof => Fact;
    }
}
