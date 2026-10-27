using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls.Models;

/// <summary>
///     Designates, or clears with a null <c>PersonId</c>, the governed workforce person who owns
///     one exact pending control revision. The person need not sign in. The signed-in recording
///     actor is kept separate, and <c>CorrelatedMemberId</c> is snapshotted only to apply
///     owner-based separation of duties; it never grants access.
/// </summary>
public sealed record ControlOwnerPersonDesignatedContent(Uuid TenantId, Uuid ProgramId,
    Uuid ControlId, Uuid VersionId, long Revision, Uuid DesignationId, Uuid? PersonId,
    Uuid? CorrelatedMemberId, string Rationale, Uuid ActorMemberId, ActorReference Actor,
    DateTimeOffset DesignatedAt) : IControlMutationContent;
