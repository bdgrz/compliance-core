using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>The fixed genesis configuration was processed for independent platform operators.</summary>
[Discriminator("bdgrz.platform.operator.bootstrap.completed", 1)]
public sealed record PlatformOperatorBootstrapCompleted(Uuid BootstrapPlanId) : DomainEvent;
