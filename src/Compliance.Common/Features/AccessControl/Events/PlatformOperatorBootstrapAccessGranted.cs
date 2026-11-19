using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>One configured user received an initial ordinary platform-operator grant.</summary>
[Discriminator("bdgrz.platform.operator.bootstrap-access.granted", 1)]
public sealed record PlatformOperatorBootstrapAccessGranted(Uuid UserId, Uuid OperationId, ulong ExpectedRevision,
    Uuid BootstrapPlanId, ActorAttribution Actor) : DomainEvent;
