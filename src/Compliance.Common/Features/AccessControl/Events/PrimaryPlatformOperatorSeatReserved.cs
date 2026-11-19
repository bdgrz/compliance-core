using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>One of two primary seats was reserved for an exact operator grant and generation.</summary>
/// <remarks>BootstrapPlanId is one identity of a qualified immutable configuration generation, not a roster or manifest.</remarks>
[Discriminator("bdgrz.platform.primary-operator.seat.reserved", 1)]
public sealed record PrimaryPlatformOperatorSeatReserved(Uuid OperationId, ulong ExpectedRevision, Uuid UserId,
    Uuid GrantEpisodeId, Uuid LineageId, ulong Generation, bool Bootstrap, Uuid BootstrapPlanId,
    Uuid ActorUserId, ActorAttribution Actor) : DomainEvent
{
    /// <summary>The initial development seat marks an already granted operator.</summary>
    public bool InitialExistingGrant { get; init; }
}
