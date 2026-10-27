using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Proofs.Models;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.AccessControl.Models;

/// <summary>Closed union of the source facts that can complete an administrator restoration or registration.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(ReinstatedProof), "member_reinstated")]
[JsonDerivedType(typeof(AssignedProof), "team_member_assigned")]
public abstract record CompleteAdministratorRestorationSourceProof
{
    private protected CompleteAdministratorRestorationSourceProof()
    {
    }

    /// <summary>The proof of whichever source fact this variant carries.</summary>
    [JsonIgnore]
    public abstract SourceFactProof Proof { get; }

    /// <summary>The member reinstatement that applied the restoration.</summary>
    public sealed record ReinstatedProof(SourceFactProof<MemberReinstated> Fact) : CompleteAdministratorRestorationSourceProof
    {
        /// <inheritdoc />
        public override SourceFactProof Proof => Fact;
    }

    /// <summary>The administrators-team assignment that applied the restoration or registration.</summary>
    public sealed record AssignedProof(SourceFactProof<TeamMemberAssigned> Fact) : CompleteAdministratorRestorationSourceProof
    {
        /// <inheritdoc />
        public override SourceFactProof Proof => Fact;
    }
}
