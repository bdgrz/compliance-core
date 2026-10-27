using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Models;

/// <summary>
///     The durable intent frozen to the content store when the tenant manager guard admits an administrator
///     transition. <paramref name="MemberRevision" /> is the member revision the source mutation applies at and
///     <paramref name="GuardGeneration" /> is the generation of the guard fact that admitted it; the two are distinct
///     scopes. <paramref name="AssignmentGeneration" /> is the generation of the assignment the transition closes
///     (removal) or opens (registration and restoration), and <paramref name="ExpectedAssignmentRevision" /> is the
///     assignment owner's revision the source mutation applies at.
/// </summary>
public sealed record AdministratorTransitionIntent(DurableIntentHeader Header, Uuid MemberId, Uuid MemberEpisodeId,
    ulong MemberRevision, Uuid? AssignmentId, ulong? AssignmentGeneration, ulong? ExpectedAssignmentRevision,
    AdministratorTransitionKind TransitionKind, string? Reason, ulong GuardGeneration,
    ImmutableSelectionManifest EligibilityRequirements)
{
    /// <summary>Schema of the frozen intent content.</summary>
    public const string Schema = "administrator-transition-intent.v1";
}
