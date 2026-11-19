using Bdgrz.Compliance.Features.Proofs.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>Records one exact activation of the platform access-policy content reference.</summary>
[Discriminator("bdgrz.access-policy.activated", 1)]
public sealed record AccessPolicyActivated(Uuid OperationId, ulong ExpectedPosition, ulong Generation,
    ImmutableContentRef Content, ActorAttribution Actor) : DomainEvent;
