using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

/// <summary>Closed union of the source facts that can complete an administrator withdrawal.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(SuspendedProof), "member_suspended")]
[JsonDerivedType(typeof(DeprovisionedProof), "member_deprovisioned")]
[JsonDerivedType(typeof(RemovedProof), "team_member_removed")]
public abstract record CompleteAdministratorWithdrawalSourceProof
{
    private protected CompleteAdministratorWithdrawalSourceProof()
    {
    }

    /// <summary>The proof of whichever source fact this variant carries.</summary>
    [JsonIgnore]
    public abstract SourceFactProof Proof { get; }

    /// <summary>The member suspension that applied the withdrawal.</summary>
    public sealed record SuspendedProof(SourceFactProof<MemberSuspended> Fact) : CompleteAdministratorWithdrawalSourceProof
    {
        /// <inheritdoc />
        public override SourceFactProof Proof => Fact;
    }

    /// <summary>The member deprovisioning that applied the withdrawal.</summary>
    public sealed record DeprovisionedProof(SourceFactProof<MemberDeprovisioned> Fact) : CompleteAdministratorWithdrawalSourceProof
    {
        /// <inheritdoc />
        public override SourceFactProof Proof => Fact;
    }

    /// <summary>The administrators-team removal that applied the withdrawal.</summary>
    public sealed record RemovedProof(SourceFactProof<TeamMemberRemoved> Fact) : CompleteAdministratorWithdrawalSourceProof
    {
        /// <inheritdoc />
        public override SourceFactProof Proof => Fact;
    }
}
